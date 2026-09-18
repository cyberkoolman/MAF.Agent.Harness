using MAF.RPTeam.Tools;

namespace MAF.RPTeam.Phases.Phase1;

public sealed class Phase1BrowserRuntime : IBrowserRuntime
{
    public Phase1BrowserRuntime()
    {
        Ember = new NullBrowserTools();
        Sentinel = new NullBrowserTools();
    }

    public bool IsAvailable => false;

    public IBrowserTools Ember { get; }

    public IBrowserTools Sentinel { get; }

    public IBrowserAcceptance? Acceptance => null;

    public async ValueTask DisposeAsync()
    {
        await Ember.DisposeAsync();
        await Sentinel.DisposeAsync();
    }
}
