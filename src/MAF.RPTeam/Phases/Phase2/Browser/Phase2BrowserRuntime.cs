using MAF.RPTeam.Tools;

namespace MAF.RPTeam.Phases.Phase2.Browser;

public sealed class Phase2BrowserRuntime : IBrowserRuntime
{
    private readonly PlaywrightBrowserHost _host;
    private readonly PlaywrightBrowserTools _acceptance;

    public Phase2BrowserRuntime(
        BrowserSettings settings,
        string frontendUrl,
        string backendUrl)
    {
        settings.Validate();
        _host = new PlaywrightBrowserHost(settings);
        Ember = new PlaywrightBrowserTools(
            _host,
            settings,
            frontendUrl,
            backendUrl,
            "ember");
        Sentinel = new PlaywrightBrowserTools(
            _host,
            settings,
            frontendUrl,
            backendUrl,
            "sentinel");
        _acceptance = new PlaywrightBrowserTools(
            _host,
            settings,
            frontendUrl,
            backendUrl,
            "acceptance");
    }

    public bool IsAvailable => true;

    public IBrowserTools Ember { get; }

    public IBrowserTools Sentinel { get; }

    public IBrowserAcceptance Acceptance => _acceptance;

    public PlaywrightBrowserTools Smoke => _acceptance;

    public async ValueTask DisposeAsync()
    {
        await Ember.DisposeAsync();
        await Sentinel.DisposeAsync();
        await _acceptance.DisposeAsync();
        await _host.DisposeAsync();
    }
}
