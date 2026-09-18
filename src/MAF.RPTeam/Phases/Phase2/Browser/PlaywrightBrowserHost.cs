using Microsoft.Playwright;

namespace MAF.RPTeam.Phases.Phase2.Browser;

internal sealed class PlaywrightBrowserHost : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly BrowserSettings _settings;
    private IPlaywright? _playwright;
    private volatile IBrowser? _browser;
    private volatile Exception? _startupFailure;
    private bool _disposed;

    public PlaywrightBrowserHost(BrowserSettings settings)
    {
        _settings = settings;
    }

    public async Task<IBrowser> GetBrowserAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_browser is not null)
        {
            return _browser;
        }

        if (_startupFailure is not null)
        {
            throw new InvalidOperationException(
                "Playwright browser startup previously failed. Restart the process after " +
                $"correcting the installation: {_startupFailure.Message}",
                _startupFailure);
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_browser is not null)
            {
                return _browser;
            }

            try
            {
                _playwright = await Playwright.CreateAsync();
                _browser = await _playwright.Chromium.LaunchAsync(
                    new BrowserTypeLaunchOptions
                    {
                        Headless = _settings.Headless,
                        Timeout = _settings.LaunchTimeoutSeconds * 1000f,
                    });
                return _browser;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _startupFailure = ex;
                if (_browser is not null)
                {
                    await _browser.CloseAsync();
                    _browser = null;
                }

                _playwright?.Dispose();
                _playwright = null;
                throw;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await _gate.WaitAsync();
        try
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_browser is not null)
            {
                await _browser.CloseAsync();
                _browser = null;
            }

            _playwright?.Dispose();
            _playwright = null;
        }
        finally
        {
            _gate.Release();
        }
    }
}
