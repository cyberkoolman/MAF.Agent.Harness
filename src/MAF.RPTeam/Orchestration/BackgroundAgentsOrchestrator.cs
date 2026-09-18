using MAF.RPTeam.Agents;
using MAF.RPTeam.Observability;
using Microsoft.Agents.AI;
using System.Diagnostics;

namespace MAF.RPTeam.Orchestration;

/// <summary>
/// Phoenix orchestrates by delegating to background agents.
///
/// The harness drives each agent round. Application code then verifies the resulting
/// artifacts because a model's completion report is not evidence.
///
/// Within each round, one <c>RunAsync</c> call drives the mission because the harness
/// pipeline is doing the work between model calls:
///
///   1. Builds the prompt: harness instructions, then agent instructions, then the
///      available skills (names and descriptions only, bodies loaded on demand), then
///      todo state and current mode, then the conversation so far.
///   2. Calls the model, and invokes whatever tools it asked for — including the six
///      background_agents_* tools that start and collect the specialists' work.
///   3. Persists history after that model call, so a crash resumes rather than restarts.
///   4. Checks the approval rules before any tool with side effects runs.
///   5. Compacts the conversation if it is approaching the token budget.
///   6. Emits OpenTelemetry spans for all of it.
///   7. Asks BackgroundTaskCompletionLoopEvaluator whether any delegated task is still
///      Running. If so, re-invokes Phoenix and repeats from step 1. If not, returns.
///
/// Step 7 is why there is no background-task polling loop in this file. The re-invocation
/// that the original demo implemented as "poll sessions_history every 10 seconds looking
/// for 'FE-001 complete'" is now a loop evaluator supplied by the framework.
///
/// The outer loop has a different purpose: MissionAcceptanceVerifier checks source,
/// fresh-process tests, live HTTP results, and the README independently of Phoenix. Failed
/// evidence is returned to the same session for a bounded corrective round. Only this
/// application layer can emit the verified completion marker.
///
/// What this replaces from the earlier prototype's custom mission controller:
///
///   Step 1  Inline Python that mutated sessions.json and deleted transcript files to
///           clear stale subagent sessions. Gone — each task gets a fresh child session
///           and background_agents_clear_completed_task releases it.
///   Step 3  A 10-second polling loop over sessions_history matching the literal string
///           "FE-001 complete", with timestamp comparison to reject stale hits from
///           earlier demo runs. Gone — task IDs and terminal statuses are structural.
///   Step 5  The same polling loop again for phase 2. Gone.
///
/// What survives is the actual choreography: announce, dispatch, gather, report — and that
/// lives in skills/mission-control/SKILL.md, as prose, where a reviewer can read it.
/// </summary>
public sealed class BackgroundAgentsOrchestrator : IMissionOrchestrator
{
    private readonly HarnessAgent _phoenix;
    private readonly MissionAcceptanceVerifier _verifier;
    private readonly int _acceptanceMaxRounds;
    private readonly bool _enforceAcceptance;

    public string Name => "background-agents";

    public BackgroundAgentsOrchestrator(
        HarnessAgent phoenix,
        MissionAcceptanceVerifier verifier,
        int acceptanceMaxRounds,
        bool enforceAcceptance)
    {
        _phoenix = phoenix;
        _verifier = verifier;
        _acceptanceMaxRounds = Math.Max(1, acceptanceMaxRounds);
        _enforceAcceptance = enforceAcceptance;
    }

    public async Task<string> RunMissionAsync(string mission, CancellationToken cancellationToken = default)
    {
        // A session is the harness's unit of durable state: conversation history, todo
        // list, current mode, file memory, and background-task metadata all hang off it.
        // Reuse one session across turns; each delegated task gets its own child session.
        var session = await _phoenix.CreateSessionAsync(cancellationToken);

        Console.WriteLine();
        Console.WriteLine("--- Phoenix is running the mission ---");
        if (_enforceAcceptance)
        {
            Console.WriteLine("Agent narration is provisional until the application acceptance gate passes.");
        }
        Console.WriteLine();

        var prompt = mission;
        for (var round = 1; round <= RequiredRounds(); round++)
        {
            var response = await _phoenix.RunAsync(
                prompt,
                session,
                cancellationToken: cancellationToken);
            if (!_enforceAcceptance)
            {
                return response.Text;
            }

            using var acceptanceActivity = MissionTelemetry.StartAcceptanceRound(round);
            var acceptance = await _verifier.VerifyAsync(cancellationToken: cancellationToken);
            acceptanceActivity?.SetTag("mission.acceptance.passed", acceptance.Passed);
            acceptanceActivity?.SetStatus(
                acceptance.Passed ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
            PrintAcceptance(round, acceptance);
            if (acceptance.Passed)
            {
                return VerifiedReport(response.Text, acceptance);
            }

            if (round == _acceptanceMaxRounds)
            {
                throw new MissionAcceptanceException(FailedReport(response.Text, acceptance, round));
            }

            prompt = acceptance.ToCorrectivePrompt();
        }

        throw new InvalidOperationException("Acceptance loop exited unexpectedly.");
    }

    /// <summary>
    /// Streaming variant, so the audience sees progress instead of a spinner.
    ///
    /// <c>RunStreamingAsync</c> is on <c>AIAgent</c>, so it works identically for a harness
    /// agent and a plain one — the harness adds capability without changing the contract.
    /// </summary>
    public async Task<string> RunMissionStreamingAsync(string mission, CancellationToken cancellationToken = default)
    {
        var session = await _phoenix.CreateSessionAsync(cancellationToken);

        Console.WriteLine();
        Console.WriteLine("--- Phoenix is running the mission ---");
        if (_enforceAcceptance)
        {
            Console.WriteLine("Agent narration is provisional until the application acceptance gate passes.");
        }
        Console.WriteLine();

        var prompt = mission;
        for (var round = 1; round <= RequiredRounds(); round++)
        {
            var transcript = await StreamRoundAsync(
                prompt,
                session,
                cancellationToken);
            if (!_enforceAcceptance)
            {
                return transcript;
            }

            using var acceptanceActivity = MissionTelemetry.StartAcceptanceRound(round);
            var acceptance = await _verifier.VerifyAsync(cancellationToken: cancellationToken);
            acceptanceActivity?.SetTag("mission.acceptance.passed", acceptance.Passed);
            acceptanceActivity?.SetStatus(
                acceptance.Passed ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
            PrintAcceptance(round, acceptance);
            if (acceptance.Passed)
            {
                return VerifiedReport(transcript, acceptance);
            }

            if (round == _acceptanceMaxRounds)
            {
                throw new MissionAcceptanceException(FailedReport(transcript, acceptance, round));
            }

            Console.WriteLine();
            Console.WriteLine($"Acceptance round {round} failed. Returning evidence to Phoenix.");
            prompt = acceptance.ToCorrectivePrompt();
        }

        throw new InvalidOperationException("Acceptance loop exited unexpectedly.");
    }

    private async Task<string> StreamRoundAsync(
        string prompt,
        AgentSession session,
        CancellationToken cancellationToken)
    {
        var transcript = new System.Text.StringBuilder();

        await foreach (var update in _phoenix.RunStreamingAsync(
                           prompt,
                           session,
                           cancellationToken: cancellationToken))
        {
            if (string.Equals(update.AuthorName, TeamFactory.HarnessAuthorName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var text = update.Text;
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            Console.Write(text);
            transcript.Append(text);
        }

        Console.WriteLine();
        return transcript.ToString();
    }

    private int RequiredRounds() => _enforceAcceptance ? _acceptanceMaxRounds : 1;

    private static void PrintAcceptance(int round, MissionAcceptanceResult acceptance)
    {
        Console.WriteLine();
        Console.WriteLine($"--- Application acceptance gate: round {round} ---");
        Console.WriteLine(acceptance.ToReport());
        Console.WriteLine();
    }

    private static string VerifiedReport(string agentReport, MissionAcceptanceResult acceptance) =>
        "Mission Complete - independently verified\n\n" +
        acceptance.ToReport() +
        "\n\nProvisional agent transcript (intermediate claims may be superseded by the acceptance evidence above):\n" +
        agentReport.Trim();

    private static string FailedReport(
        string agentReport,
        MissionAcceptanceResult acceptance,
        int rounds) =>
        $"Mission failed application acceptance after {rounds} rounds.\n\n" +
        acceptance.ToReport() +
        "\n\nLast agent report:\n" +
        agentReport.Trim();
}
