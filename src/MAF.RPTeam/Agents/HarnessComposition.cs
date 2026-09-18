using Microsoft.Agents.AI;

namespace MAF.RPTeam.Agents;

/// <summary>
/// Prints the harness capability matrix at startup, read from the actual
/// <see cref="HarnessAgentOptions"/> instance rather than hard-coded.
///
/// This exists because the harness is invisible otherwise. Nothing in the console output
/// of a normal run tells you that planning, per-model-call persistence, compaction,
/// approvals and telemetry are active — the agent just works, and it reads as though the
/// application did it. Printing the composition makes the point that the pipeline came
/// from the framework and not from this repository.
///
/// It also doubles as a demo aid: when someone asks "what is a harness, concretely?",
/// this table answers in twelve lines.
/// </summary>
public static class HarnessComposition
{
    public static void Print(HarnessAgentOptions options)
    {
        Console.WriteLine("Agent Harness composition (phoenix)");
        Console.WriteLine("  Each row is supplied by Microsoft.Agents.AI.Harness, not by this project.");
        Console.WriteLine();

        Row("function invocation", $"on, ceiling {Show(options.MaximumIterationsPerRequest)} iterations/request");
        Row("history persistence", "on, after every model call (not every turn)");
        Row("compaction", options.DisableCompaction
            ? "off"
            : $"on, window {Show(options.MaxContextWindowTokens)} tokens");
        Row("todo tracking", Flag(!options.DisableTodoProvider));
        Row("agent modes (plan/execute)", Flag(!options.DisableAgentModeProvider));
        Row("file memory", Flag(!options.DisableFileMemory));
        Row("tool approval", options.ToolApprovalAgentOptions?.AutoApprovalRules is not null
            ? "on, auto-approval rules supplied"
            : "on, no auto-approval (will prompt)");
        Row("opentelemetry", options.DisableOpenTelemetry
            ? "off"
            : $"on, source '{options.OpenTelemetrySourceName ?? "default"}'");
        Row("agent skills", options.AgentSkillsSource is null
            ? "no source"
            : "on, file-based (agentskills.io SKILL.md)");
        Row("web search", Flag(!options.DisableWebSearch));
        Row("background agents", options.BackgroundAgents is null
            ? "none"
            : $"on, {options.BackgroundAgents.Count()} children: {string.Join(", ", options.BackgroundAgents.Select(a => a.Name))}");
        Row("bounded looping", options.LoopEvaluators is null || !options.LoopEvaluators.Any()
            ? "off"
            : $"on, max {Show(options.LoopAgentOptions?.MaxIterations)} iterations");

        Console.WriteLine();
    }

    private static void Row(string capability, string state) =>
        Console.WriteLine($"  {capability,-28} {state}");

    private static string Flag(bool enabled) => enabled ? "on" : "off";

    private static string Show(int? value) => value?.ToString() ?? "default";
}
