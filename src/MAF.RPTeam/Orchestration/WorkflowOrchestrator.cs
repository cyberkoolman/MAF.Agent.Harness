using Microsoft.Agents.AI;

namespace MAF.RPTeam.Orchestration;

/// <summary>
/// NOT IMPLEMENTED - Phase 1 placeholder for the recommended production shape.
///
/// The Mission Control choreography is a fixed graph, not open-ended coordination:
///
///     Phase 1:  ember  ||  forge        (concurrent)
///                     |
///     Phase 2:  sentinel || scroll      (concurrent, after phase 1)
///                     |
///     Report                            (sequential)
///
/// That is exactly what the sequential and concurrent orchestration patterns express, and
/// those reached 1.0 - whereas background agents are still marked experimental. Expressing
/// the phases as a workflow also brings checkpoint/resume and human-in-the-loop for free,
/// and makes the demo repeatable rather than dependent on a planner's mood.
///
/// The cost: Phoenix stops being an agent with agency and becomes a graph. For a scripted
/// stage demo that is a feature. If the point of the demo is "Phoenix decides", keep
/// BackgroundAgentsOrchestrator instead.
///
/// TO IMPLEMENT:
///   1. Add the orchestrations package (Python calls it agent-framework-orchestrations;
///      confirm the .NET package and builder type names against the samples at
///      github.com/microsoft/agent-framework/tree/main/dotnet/samples/03-workflows).
///   2. Build a concurrent orchestration over [ember, forge], then a second over
///      [sentinel, scroll], then compose the two sequentially.
///   3. Feed phase 1 output into phase 2 input so Sentinel and Scroll see what changed.
///   4. Keep telemetry and acceptance at the phase boundaries.
///
/// The builder API names were not verified when this scaffold was written - do not assume
/// the signatures without checking the samples first.
/// </summary>
public sealed class WorkflowOrchestrator : IMissionOrchestrator
{
    private readonly IReadOnlyList<AIAgent> _specialists;

    public string Name => "workflow";

    public WorkflowOrchestrator(IReadOnlyList<AIAgent> specialists) => _specialists = specialists;

    public Task<string> RunMissionAsync(string mission, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException(
            "Workflow orchestration is the recommended Phase 2 target. See the class comment " +
            "and docs/ORCHESTRATION.md. Run with --orchestrator background-agents for now.");
}
