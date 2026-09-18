namespace MAF.RPTeam.Orchestration;

/// <summary>
/// The orchestration seam.
///
/// Two implementations are anticipated, and the choice is the main open architectural
/// decision in this port:
///
///   BackgroundAgentsOrchestrator  Phoenix holds delegation tools and decides. Closest to
///                                 the original OpenClaw design. Background agents are
///                                 marked experimental in Agent Framework.
///
///   WorkflowOrchestrator          The two-phase graph expressed with the concurrent and
///                                 sequential orchestration builders, which reached 1.0.
///                                 Deterministic, checkpointable, human-in-the-loop capable.
///                                 Phoenix loses agency but the demo becomes repeatable.
///
/// See docs/ORCHESTRATION.md for the trade-off and a recommendation.
/// </summary>
public interface IMissionOrchestrator
{
    string Name { get; }

    /// <summary>Runs the mission to completion and returns the final report text.</summary>
    Task<string> RunMissionAsync(string mission, CancellationToken cancellationToken = default);
}
