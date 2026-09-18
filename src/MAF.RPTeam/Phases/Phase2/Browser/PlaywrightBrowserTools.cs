using System.ComponentModel;
using System.Text;
using MAF.RPTeam.Tools;
using Microsoft.Extensions.AI;
using Microsoft.Playwright;

namespace MAF.RPTeam.Phases.Phase2.Browser;

public sealed class PlaywrightBrowserTools :
    IBrowserTools,
    IBrowserAcceptance
{
    private const string ActionableSelector =
        "a[href],button,input:not([type=hidden]),select,textarea," +
        "[role=button],[role=link],[role=checkbox],[role=radio]," +
        "[role=menuitem],[tabindex]:not([tabindex='-1'])";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly PlaywrightBrowserHost _host;
    private readonly BrowserSettings _settings;
    private readonly BrowserUrlPolicy _urlPolicy;
    private readonly BrowserDiagnosticBuffer _diagnostics;
    private readonly BrowserReferenceRegistry _references = new();
    private readonly string _frontendUrl;
    private readonly string _consumerName;

    private IBrowserContext? _context;
    private IPage? _page;
    private bool _hasDocument;
    private bool _disposed;

    internal PlaywrightBrowserTools(
        PlaywrightBrowserHost host,
        BrowserSettings settings,
        string frontendUrl,
        string backendUrl,
        string consumerName)
    {
        _host = host;
        _settings = settings;
        _frontendUrl = frontendUrl;
        _consumerName = consumerName;
        _urlPolicy = new BrowserUrlPolicy(frontendUrl, backendUrl);
        _diagnostics = new BrowserDiagnosticBuffer(settings.ConsoleCapacity);
    }

    public bool IsAvailable => true;

    public IList<AITool> AsTools() =>
    [
        AIFunctionFactory.Create(BrowserNavigate),
        CreateScreenshotTool(),
        AIFunctionFactory.Create(BrowserConsole),
        AIFunctionFactory.Create(BrowserSnapshot),
        AIFunctionFactory.Create(BrowserClick),
    ];

    public IList<AITool> VisionSmokeTools() => [CreateScreenshotTool()];

    [Description("Navigate the browser to a URL on the configured frontend origin.")]
    public async Task<string> BrowserNavigate(
        [Description("Absolute frontend URL.")] string url,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await NavigateCoreAsync(url, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    [Description("Capture the current browser viewport as real PNG image content.")]
    public async Task<DataContent> BrowserScreenshot(
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var page = await EnsureStartedAsync(cancellationToken);
            EnsurePageWasNavigated(page);

            var png = await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Type = ScreenshotType.Png,
                FullPage = false,
                Animations = ScreenshotAnimations.Disabled,
                Timeout = ActionTimeoutMilliseconds,
            });

            cancellationToken.ThrowIfCancellationRequested();
            return new DataContent(png, "image/png")
            {
                Name = "browser-screenshot.png",
            };
        }
        finally
        {
            _gate.Release();
        }
    }

    [Description("Read bounded browser console messages and uncaught page errors.")]
    public async Task<string> BrowserConsole(
        [Description("Minimum level: debug, info, warning, or error.")] string level = "error",
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureStartedAsync(cancellationToken);
            return _diagnostics.Format(level);
        }
        finally
        {
            _gate.Release();
        }
    }

    [Description("Read a bounded accessibility snapshot and stable clickable references.")]
    public async Task<string> BrowserSnapshot(
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var page = await EnsureStartedAsync(cancellationToken);
            EnsurePageWasNavigated(page);
            _references.Invalidate();

            var aria = await page.AriaSnapshotAsync(new PageAriaSnapshotOptions
            {
                Mode = AriaSnapshotMode.Ai,
                Depth = 12,
                Boxes = false,
                Timeout = ActionTimeoutMilliseconds,
            });

            if (aria.Length > _settings.MaxSnapshotCharacters)
            {
                aria = aria[.._settings.MaxSnapshotCharacters] +
                    "\n... accessibility snapshot truncated ...";
            }

            var actionables = page.Locator(ActionableSelector);
            var count = await actionables.CountAsync();
            var sb = new StringBuilder(aria);
            sb.AppendLine().AppendLine().AppendLine("Actionable elements:");

            var added = 0;
            for (var index = 0;
                 index < count && added < _settings.MaxActionableElements;
                 index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var locator = actionables.Nth(index);
                if (!await locator.IsVisibleAsync())
                {
                    continue;
                }

                var description = await locator.EvaluateAsync<string>(
                    """
                    element => {
                      const label =
                        element.getAttribute('aria-label') ||
                        element.innerText ||
                        element.getAttribute('value') ||
                        element.getAttribute('name') ||
                        '';
                      const role =
                        element.getAttribute('role') ||
                        element.tagName.toLowerCase();
                      return `${role}: ${label.replace(/\s+/g, ' ').trim()}`;
                    }
                    """);
                var reference = _references.Add(locator);
                sb.Append("- [")
                    .Append(reference)
                    .Append("] ")
                    .AppendLine(string.IsNullOrWhiteSpace(description)
                        ? "interactive element"
                        : description);
                added++;
            }

            if (added == 0)
            {
                sb.AppendLine("- none");
            }

            return sb.ToString();
        }
        finally
        {
            _gate.Release();
        }
    }

    [Description("Click a stable reference from the latest BrowserSnapshot.")]
    public async Task<string> BrowserClick(
        [Description("Reference such as ref-1.")] string reference,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var page = await EnsureStartedAsync(cancellationToken);
            var locator = _references.Resolve(reference);
            if (!await locator.IsVisibleAsync())
            {
                throw new InvalidOperationException(
                    $"Browser reference '{reference}' is no longer visible. " +
                    "Run BrowserSnapshot again.");
            }

            await locator.ClickAsync(new LocatorClickOptions
            {
                Force = false,
                Timeout = ActionTimeoutMilliseconds,
            });
            cancellationToken.ThrowIfCancellationRequested();
            _references.Invalidate();

            return $"Clicked {reference}. Current page: {page.Url}; title: {await page.TitleAsync()}.";
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<BrowserAcceptanceResult> VerifyFrontendAsync(
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var page = await EnsureStartedAsync(cancellationToken);
            await NavigateCoreAsync(_frontendUrl, cancellationToken);

            var navbar = page.Locator("nav.navbar");
            await navbar.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = ActionTimeoutMilliseconds,
            });

            var title = await page.Locator(".navbar-title").InnerTextAsync();
            var navigationItems = await page.Locator(".navbar-links button").CountAsync();
            var errorDiagnostics = _diagnostics.Read("error");
            var png = await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Type = ScreenshotType.Png,
                FullPage = false,
                Animations = ScreenshotAnimations.Disabled,
                Timeout = ActionTimeoutMilliseconds,
            });

            var passed =
                string.Equals(title.Trim(), "Mission Control", StringComparison.Ordinal) &&
                navigationItems == 3 &&
                errorDiagnostics.Count == 0 &&
                png.Length > 0;

            var detail =
                $"Navbar visible; title='{title.Trim()}'; navigation items={navigationItems}; " +
                $"browser errors={errorDiagnostics.Count}; screenshot bytes={png.Length}." +
                FormatAcceptanceErrors(errorDiagnostics);

            return new BrowserAcceptanceResult(passed, detail);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new BrowserAcceptanceResult(
                false,
                $"Playwright visual verification failed for {_consumerName}: {ex.Message}");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string> RunSmokeAsync(
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var page = await EnsureStartedAsync(cancellationToken);
            await NavigateCoreAsync(_frontendUrl, cancellationToken);
            var png = await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Type = ScreenshotType.Png,
                FullPage = false,
                Animations = ScreenshotAnimations.Disabled,
                Timeout = ActionTimeoutMilliseconds,
            });
            var rootAttached = await page.Locator("#root").CountAsync() > 0;

            if (!rootAttached || png.Length == 0)
            {
                throw new InvalidOperationException(
                    "Browser smoke did not find #root or capture a screenshot.");
            }

            return $"Chromium ready; URL={page.Url}; title='{await page.TitleAsync()}'; " +
                $"screenshot bytes={png.Length}; browser errors={_diagnostics.Read("error").Count}.";
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string> PrepareVisionSmokeAsync(
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var page = await EnsureStartedAsync(cancellationToken);
            var marker = $"P2-{Guid.NewGuid():N}"[..11].ToUpperInvariant();
            await page.SetContentAsync(
                $"""
                 <!doctype html>
                 <html aria-label="Visual verification canvas">
                   <head><title>Visual verification</title></head>
                   <body style="margin:0;background:#111827;color:#f9fafb;display:grid;place-items:center;height:100vh;font-family:Arial,sans-serif">
                     <canvas id="proof" width="1200" height="600" aria-label="Phase 2 image proof"></canvas>
                     <script>
                       const canvas = document.getElementById('proof');
                       const context = canvas.getContext('2d');
                       context.fillStyle = '#172554';
                       context.fillRect(0, 0, canvas.width, canvas.height);
                       context.strokeStyle = '#22d3ee';
                       context.lineWidth = 16;
                       context.strokeRect(8, 8, canvas.width - 16, canvas.height - 16);
                       context.textAlign = 'center';
                       context.fillStyle = '#f9fafb';
                       context.font = '32px Arial';
                       context.fillText('PHASE 2 VISION CODE', 600, 210);
                       context.fillStyle = '#fde047';
                       context.font = 'bold 112px Arial';
                       context.fillText('{marker}', 600, 380);
                     </script>
                   </body>
                 </html>
                 """,
                new PageSetContentOptions
                {
                    WaitUntil = WaitUntilState.DOMContentLoaded,
                    Timeout = ActionTimeoutMilliseconds,
                });
            _diagnostics.Clear();
            _references.Invalidate();
            _hasDocument = true;
            return marker;
        }
        finally
        {
            _gate.Release();
        }
    }

    private float ActionTimeoutMilliseconds => _settings.ActionTimeoutSeconds * 1000f;

    private float NavigationTimeoutMilliseconds => _settings.NavigationTimeoutSeconds * 1000f;

    private async Task<IPage> EnsureStartedAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_page is not null)
        {
            return _page;
        }

        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var browser = await _host.GetBrowserAsync(cancellationToken);
            _context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                AcceptDownloads = false,
                ServiceWorkers = ServiceWorkerPolicy.Block,
                ViewportSize = new ViewportSize
                {
                    Width = _settings.ViewportWidth,
                    Height = _settings.ViewportHeight,
                },
            });
            _context.SetDefaultTimeout(ActionTimeoutMilliseconds);
            _context.SetDefaultNavigationTimeout(NavigationTimeoutMilliseconds);
            await _context.RouteAsync("**/*", async route =>
            {
                var allowed = route.Request.IsNavigationRequest
                    ? _urlPolicy.IsAllowedNavigation(route.Request.Url)
                    : _urlPolicy.IsAllowedRequest(route.Request.Url);
                if (!allowed)
                {
                    await route.AbortAsync();
                    return;
                }

                await route.ContinueAsync();
            });
            var page = await _context.NewPageAsync();
            _page = page;
            page.Console += (_, message) =>
            {
                var type = message.Type;
                if (type == "error" &&
                    Uri.TryCreate(message.Location, UriKind.Absolute, out var location) &&
                    location.Scheme is "http" or "https" &&
                    !_urlPolicy.IsAllowedRequest(message.Location))
                {
                    type = "warning";
                }

                _diagnostics.Add(new BrowserDiagnostic(
                    DateTimeOffset.UtcNow,
                    string.IsNullOrWhiteSpace(message.Location) ? page.Url : message.Location,
                    type,
                    "console",
                    message.Text));
            };
            page.PageError += (_, error) =>
                _diagnostics.Add(new BrowserDiagnostic(
                    DateTimeOffset.UtcNow,
                    page.Url,
                    "error",
                    "page",
                    error));

            return page;
        }
        catch
        {
            await ResetSessionAsync();
            throw;
        }
    }

    private async Task<string> NavigateCoreAsync(
        string url,
        CancellationToken cancellationToken)
    {
        var target = _urlPolicy.ValidateNavigation(url);
        var page = await EnsureStartedAsync(cancellationToken);
        _diagnostics.Clear();
        _references.Invalidate();
        _hasDocument = false;

        IResponse? response;
        try
        {
            response = await page.GotoAsync(target.AbsoluteUri, new PageGotoOptions
            {
                Timeout = NavigationTimeoutMilliseconds,
                WaitUntil = WaitUntilState.DOMContentLoaded,
            });
        }
        catch
        {
            await ResetSessionAsync();
            throw;
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (response is { Ok: false })
        {
            await ResetSessionAsync();
            throw new InvalidOperationException(
                $"Browser navigation returned HTTP {response.Status} {response.StatusText}.");
        }

        try
        {
            _urlPolicy.ValidateNavigation(page.Url);
        }
        catch
        {
            await ResetSessionAsync();
            throw;
        }

        await page.Locator("#root").WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Attached,
            Timeout = ActionTimeoutMilliseconds,
        });
        await page.WaitForTimeoutAsync(500);
        _hasDocument = true;

        return $"Navigated to {page.Url}; title: {await page.TitleAsync()}.";
    }

    private void EnsurePageWasNavigated(IPage page)
    {
        if (!_hasDocument)
        {
            throw new InvalidOperationException(
                "Navigate to the frontend before using this browser tool.");
        }
    }

    private AITool CreateScreenshotTool() =>
        AIFunctionFactory.Create(
            BrowserScreenshot,
            new AIFunctionFactoryOptions
            {
                ExcludeResultSchema = true,
            });

    private static string FormatAcceptanceErrors(
        IReadOnlyList<BrowserDiagnostic> errors)
    {
        if (errors.Count == 0)
        {
            return string.Empty;
        }

        var detail = string.Join(
            " | ",
            errors.Take(3).Select(error =>
                $"{error.Kind} at {error.Url}: {error.Message}"));
        if (detail.Length > 600)
        {
            detail = detail[..600] + "...";
        }

        return $" Errors: {detail}";
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
            await ResetSessionAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task ResetSessionAsync()
    {
        _references.Invalidate();
        _hasDocument = false;

        if (_page is not null)
        {
            await _page.CloseAsync();
            _page = null;
        }

        if (_context is not null)
        {
            await _context.CloseAsync();
            _context = null;
        }
    }
}
