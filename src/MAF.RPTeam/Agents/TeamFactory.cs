using MAF.RPTeam.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace MAF.RPTeam.Agents;

/// <summary>
/// Builds the five Mission Control agents.
///
/// This file is where the port stops being "an app that calls a model" and becomes
/// "an app built on an agent harness". Two different agent kinds are created here, and
/// the difference is the whole point:
///
///   CreateSpecialists()  -> ChatClientAgent. A chat client plus tools, its checked-in
///                           SKILL.md operating procedure, and a function-invocation loop.
///                           No planning, persistence, compaction, or approvals.
///
///   CreatePhoenix()      -> HarnessAgent. The same chat client, wrapped in the harness
///                           pipeline: planning and todo state, per-model-call history
///                           persistence, context compaction, tool approvals, skills
///                           discovery, OpenTelemetry, and bounded re-invocation.
///
/// Everything Phoenix does that Ember cannot is a harness capability, not code in this
/// repository. See Agent.Harness.md.
///
/// Child agents are plain agents on purpose: background-agent children cannot proxy an
/// interactive tool-approval request back to the parent, so a child wrapped in the
/// harness's approval middleware would deadlock waiting for an approver that cannot
/// reach it. Their blast radius is bounded by WorkspaceScope instead.
/// </summary>
public sealed class TeamFactory : IDisposable
{
    private readonly IChatClient _chatClient;
    private readonly MissionSettings _settings;
    private readonly IBrowserTools _emberBrowser;
    private readonly IBrowserTools _sentinelBrowser;
    private readonly HttpTools _http;

    public TeamFactory(
        IChatClient chatClient,
        MissionSettings settings,
        IBrowserTools emberBrowser,
        IBrowserTools sentinelBrowser)
    {
        _chatClient = chatClient;
        _settings = settings;
        _emberBrowser = emberBrowser;
        _sentinelBrowser = sentinelBrowser;
        _http = new HttpTools(loopbackOnly: true);
    }

    /// <summary>
    /// The four specialists Phoenix delegates to.
    ///
    /// MAF surface used here: <c>IChatClient.AsAIAgent(...)</c> from
    /// <c>Microsoft.Agents.AI.ChatClientExtensions</c>. Returns a <c>ChatClientAgent</c>,
    /// which derives from <c>AIAgent</c> — the same base type as <c>HarnessAgent</c>. That
    /// shared base is why these four can be handed to Phoenix as background agents without
    /// any adapter: to the harness, a child agent is just an AIAgent.
    ///
    /// Names must be non-empty and unique case-insensitively — the background-agents
    /// provider addresses children by name in the tool calls it exposes to the model.
    /// </summary>
    public IReadOnlyList<AIAgent> CreateSpecialists(string skillsPath)
    {
        var frontend = new WorkspaceScope(_settings.WorkspaceRoot, "frontend", "frontend");
        var backend = new WorkspaceScope(_settings.WorkspaceRoot, "backend", "backend");
        var whole = new WorkspaceScope(_settings.WorkspaceRoot, "workspace");

        // Ember: frontend files, plus browser tools when available.
        var emberTools = new WorkspaceTools(frontend, ["node", "npx", "npm", "git"]).AsTools();
        var ember = _chatClient.AsAIAgent(
            instructions: WithSkill(Personas.Ember, skillsPath, "delegate-ember"),
            name: "ember",
            description: "Frontend developer. Fixes React and UI bugs and verifies the result.",
            tools: Combine(emberTools, _emberBrowser.AsTools(), _http.AsTools()));

        // Forge: backend files, plus HTTP for reproduce-and-verify.
        var forgeTools = new WorkspaceTools(backend, ["node", "npx", "npm", "git"]).AsTools();
        var forge = _chatClient.AsAIAgent(
            instructions: WithSkill(Personas.Forge, skillsPath, "delegate-forge"),
            name: "forge",
            description: "Backend developer. Fixes Express route and server bugs, verified over HTTP.",
            tools: Combine(forgeTools, _http.AsTools()));

        // Sentinel: writes tests under backend, runs the suite.
        var sentinelTools = new WorkspaceTools(backend, ["npx", "npm", "node", "git"]).AsTools();
        var sentinel = _chatClient.AsAIAgent(
            instructions: WithSkill(Personas.Sentinel, skillsPath, "delegate-sentinel"),
            name: "sentinel",
            description: "QA lead. Writes and runs the Jest suite, covering the error paths.",
            tools: Combine(sentinelTools, _sentinelBrowser.AsTools(), _http.AsTools()));

        // Scroll: reads the whole workspace, writes docs, runs git and the test suite.
        var scrollTools = new WorkspaceTools(whole, ["git", "npx", "npm", "node"]).AsTools();
        var scroll = _chatClient.AsAIAgent(
            instructions: WithSkill(Personas.Scroll, skillsPath, "delegate-scroll"),
            name: "scroll",
            description: "Technical writer. Documents what changed after reading the code and git log.",
            tools: Combine(scrollTools, _http.AsTools()));

        return [ember, forge, sentinel, scroll];
    }

    /// <summary>
    /// Phoenix — the harness agent.
    ///
    /// Every option below is a named harness capability. They are written out explicitly
    /// even where the value matches the default, because the defaults are the substance of
    /// what the harness gives you and leaving them implicit hides the entire point of using
    /// it. In production code you would delete most of this and let the defaults stand;
    /// here the verbosity is the documentation.
    ///
    /// Note what is NOT here: no manual tool-call loop, no history serialisation, no token
    /// counting or truncation, no approval bookkeeping, no span emission, no retry ceiling.
    /// Those are the harness pipeline. Roughly the work this file would otherwise be.
    /// </summary>
    public HarnessAgentOptions BuildPhoenixOptions(IReadOnlyList<AIAgent> specialists, string skillsPath) =>
        new()
        {
            // ── Identity ──────────────────────────────────────────────────────────────
            Name = "phoenix",
            Description = "Team lead. Decomposes the mission and delegates to the specialists.",

            // HarnessInstructions sits ahead of ChatOptions.Instructions in the prompt.
            // The split matters: harness-level guidance describes how to operate the
            // pipeline (delegate, don't code), agent-level instructions describe the role.
            // Phoenix must always receive the fixed phase graph and exact demo endpoints.
            // Agent Skills are progressive, so relying on the model to decide to load the
            // mission-control skill can turn a known repair mission into generic exploration.
            HarnessInstructions = WithSkill(Personas.Phoenix, skillsPath, "mission-control"),

            // ── Capability 1: function invocation ─────────────────────────────────────
            // The harness drives the tool-calling loop. MaximumIterationsPerRequest is the
            // safety ceiling — the mechanism behind Microsoft's benchmark result where MAF
            // halted a runaway agent at 40 iterations and competitors had no stop at all.
            MaximumIterationsPerRequest = 40,

            // ── Capability 2: per-service-call history persistence ────────────────────
            // On by default. History is persisted after EVERY model call, not every turn,
            // which is what makes a 25-minute run resumable rather than restartable.
            // Supply a ChatHistoryProvider to control where it lands; the default is fine
            // for a demo.

            // ── Capability 3: compaction ──────────────────────────────────────────────
            // Enabled once a token budget is supplied. Long tool-calling runs would
            // otherwise walk off the end of the context window mid-mission. This single
            // property replaces OpenClaw's "compaction": { "mode": "safeguard" }.
            MaxContextWindowTokens = 128_000,
            MaxOutputTokens = 16_384,
            DisableCompaction = false,

            // ── Capability 4: todo tracking ───────────────────────────────────────────
            // On by default. Phoenix maintains a durable task list across model calls, so
            // "which of the four agents have reported back" survives context churn without
            // any code here tracking it.
            DisableTodoProvider = false,

            // ── Capability 5: agent modes (plan / execute) ────────────────────────────
            // On by default. Phoenix can plan before acting and switch modes mid-run.
            DisableAgentModeProvider = false,

            // ── Capability 6: file memory ────────────────────────────────────────────
            // On by default: durable session notes that outlive a single turn.
            DisableFileMemory = false,

            // ── Capability 7: tool approval ───────────────────────────────────────────
            // The approval middleware is always on; what changes is the rules. The default
            // options auto-approve NOTHING, which would stall an unattended demo on the
            // first tool call. AllToolsAutoApprovalRule is the documented escape hatch.
            //
            // For a governance demo, swap in ReadOnlyToolsAutoApprovalRule and let the
            // mutating calls actually prompt. That is arguably the better customer story:
            // the approval gate is real, not decorative.
            ToolApprovalAgentOptions = new ToolApprovalAgentOptions
            {
                AutoApprovalRules = [AgentSkillsProvider.AllToolsAutoApprovalRule],
            },

            // ── Capability 8: OpenTelemetry ───────────────────────────────────────────
            // On by default. No exporter wiring lives in this project — set an Application
            // Insights connection string and every model call, tool call and delegation
            // becomes a span. This is the single biggest gap versus the OpenClaw original,
            // where the only trace was Markdown files on disk.
            DisableOpenTelemetry = false,
            OpenTelemetrySourceName = "MAF.RPTeam",

            // ── Capability 9: agent skills ────────────────────────────────────────────
            // Discovers SKILL.md directories per the agentskills.io specification, and
            // loads them progressively — the model sees names and descriptions first, then
            // calls load_skill for the body only when a task matches. That is why the
            // skills/ folder ported over from OpenClaw almost unchanged: same open format.
            AgentSkillsSource = new AgentFileSkillsSource(skillsPath),
            DisableAgentSkillsProvider = false,

            // ── Capability 10: web search ─────────────────────────────────────────────
            // Added automatically where the chat client supports it. Phoenix coordinates
            // local agents and has no reason to search the web, so it is off.
            DisableWebSearch = true,

            // ── Capability 11: background agents ─────────────────────────────────────
            // The delegation core. Registering the four specialists here makes the harness
            // expose background_agents_start_task / wait_for_first_completion /
            // get_task_results / get_all_tasks / continue_task / clear_completed_task to
            // the model. Those six tools are the entire replacement for the original
            // demo's sessions_send plus 10-second keyword polling plus stale-session
            // cleanup script. Task state is real state: running, completed, failed, lost.
            //
            // Still marked EXPERIMENTAL in Agent Framework — see docs/ORCHESTRATION.md
            // for the generally available alternative.
            BackgroundAgents = specialists,

            // ── Capability 12: bounded looping ───────────────────────────────────────
            // BackgroundTaskCompletionLoopEvaluator re-invokes Phoenix while any delegated
            // task is still Running, and stops once every task is terminal. This is what
            // replaced the original's "check every 10 seconds, and ping anyone silent for
            // more than 5 minutes" heartbeat. MaxIterations is the hard stop so a stuck
            // child cannot loop forever.
            // Observed working: in testing Phoenix announced "Task assigned to Ember,
            // awaiting her report" and tried to finish while the child was still running.
            // The evaluator caught it and forced another iteration. That is the capability
            // which replaced the original demo's 10-second sessions_history polling.
            //
            // The evaluator injects its nudge as a message authored on behalf of the
            // harness. ExcludeOnBehalfOfMessages keeps that scaffolding out of the response
            // stream while still feeding it to the model - without this, framework prose
            // ("You still have 1 background task(s) running...") appears in the transcript
            // as though Phoenix had said it.
            LoopEvaluators = [new BackgroundTaskCompletionLoopEvaluator()],
            LoopAgentOptions = new LoopAgentOptions
            {
                MaxIterations = 24,
                OnBehalfOfAuthorName = HarnessAuthorName,
                ExcludeOnBehalfOfMessages = true,
            },
        };

    /// <summary>
    /// Author name stamped on messages the harness injects on Phoenix's behalf. The
    /// orchestrator filters these from console output as a second line of defence.
    /// </summary>
    public const string HarnessAuthorName = "harness";

    /// <summary>
    /// Materialises Phoenix from the options above.
    ///
    /// <c>AsHarnessAgent</c> is an extension on <c>IChatClient</c> from
    /// <c>Microsoft.Agents.AI.Harness</c>. Equivalent to
    /// <c>new HarnessAgent(chatClient, options)</c>.
    ///
    /// The returned <c>HarnessAgent</c> derives from <c>AIAgent</c> — the same base type as
    /// the four plain specialists. The harness composes existing framework building blocks
    /// rather than defining a parallel runtime, which is why adopting it is a choice of
    /// defaults rather than an architectural commitment. Every capability above has an
    /// off-switch, and turning them all off leaves you with an ordinary agent.
    /// </summary>
    public HarnessAgent CreatePhoenix(IReadOnlyList<AIAgent> specialists, string skillsPath) =>
        _chatClient.AsHarnessAgent(BuildPhoenixOptions(specialists, skillsPath));

    private static IList<AITool> Combine(params IList<AITool>[] sets)
    {
        var all = new List<AITool>();
        foreach (var set in sets)
        {
            all.AddRange(set);
        }

        return all;
    }

    private static string WithSkill(string persona, string skillsPath, string skillName)
    {
        var skillFile = Path.Combine(skillsPath, skillName, "SKILL.md");
        if (!File.Exists(skillFile))
        {
            throw new FileNotFoundException(
                $"Required operating procedure was not found for '{skillName}'.",
                skillFile);
        }

        var skill = File.ReadAllText(skillFile).Replace("\r\n", "\n");
        if (skill.StartsWith("---\n", StringComparison.Ordinal))
        {
            var frontmatterEnd = skill.IndexOf("\n---\n", 4, StringComparison.Ordinal);
            if (frontmatterEnd >= 0)
            {
                skill = skill[(frontmatterEnd + 5)..];
            }
        }

        return $"{persona}\n\nFollow this operating procedure exactly:\n\n{skill.Trim()}";
    }

    public void Dispose() => _http.Dispose();
}
