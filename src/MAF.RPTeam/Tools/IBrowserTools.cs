using Microsoft.Extensions.AI;

namespace MAF.RPTeam.Tools;

/// <summary>
/// Seam for the <c>openclaw browser</c> equivalent.
///
/// Phase 1 supplies explicit fallback tools. Phase 2 supplies isolated Playwright tools
/// for Ember and Sentinel through the same agent-facing contract.
/// </summary>
public interface IBrowserTools : IAsyncDisposable
{
    /// <summary>True when real browser automation is available.</summary>
    bool IsAvailable { get; }

    IList<AITool> AsTools();
}

public interface IBrowserAcceptance
{
    Task<BrowserAcceptanceResult> VerifyFrontendAsync(
        CancellationToken cancellationToken = default);
}

public sealed record BrowserAcceptanceResult(bool Passed, string Detail);

public interface IBrowserRuntime : IAsyncDisposable
{
    bool IsAvailable { get; }

    IBrowserTools Ember { get; }

    IBrowserTools Sentinel { get; }

    IBrowserAcceptance? Acceptance { get; }
}
