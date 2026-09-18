using System.ComponentModel;
using MAF.RPTeam.Tools;
using Microsoft.Extensions.AI;

namespace MAF.RPTeam.Phases.Phase1;

/// <summary>
/// Phase 1 stand-in for browser automation.
///
/// Rather than omitting the tools (which would leave the agent guessing why its
/// instructions mention a browser), this registers them and returns an explicit
/// "not available, verify in text instead" result. The agent then degrades to
/// reading the file and asserting on the rendered markup, which is enough to fix
/// and confirm the planted Navbar bug without a real browser.
/// </summary>
public sealed class NullBrowserTools : IBrowserTools
{
    public bool IsAvailable => false;

    public IList<AITool> AsTools() => new List<AITool>
    {
        AIFunctionFactory.Create(BrowserNavigate),
        AIFunctionFactory.Create(BrowserScreenshot),
        AIFunctionFactory.Create(BrowserConsole),
        AIFunctionFactory.Create(BrowserSnapshot),
        AIFunctionFactory.Create(BrowserClick),
    };

    private const string NotAvailable =
        "Browser automation is not enabled in this build (Phase 1). " +
        "Verify in text instead: read the source file, and use HttpGet against the dev server " +
        "to confirm the page is served. Report clearly that verification was textual, not visual.";

    [Description("Navigate the browser to a URL. Not available in this build.")]
    public string BrowserNavigate(
        [Description("Absolute URL.")] string url) => NotAvailable;

    [Description("Capture a screenshot of the current page. Not available in this build.")]
    public string BrowserScreenshot() => NotAvailable;

    [Description("Read browser console messages. Not available in this build.")]
    public string BrowserConsole(
        [Description("Minimum level, for example error.")] string level = "error") => NotAvailable;

    [Description("Read an accessibility-oriented snapshot. Not available in this build.")]
    public string BrowserSnapshot() => NotAvailable;

    [Description("Click a stable reference from the latest browser snapshot. Not available in this build.")]
    public string BrowserClick(
        [Description("Reference such as ref-1.")] string reference) => NotAvailable;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
