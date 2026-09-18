using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;

namespace MAF.RPTeam.Phases.Phase2.Browser;

public static class BrowserContractSmoke
{
    public static async Task<string> RunAsync(
        Phase2BrowserRuntime runtime,
        CancellationToken cancellationToken = default)
    {
        var browser = runtime.Smoke;
        var startup = await browser.RunSmokeAsync(cancellationToken);
        var console = await browser.BrowserConsole("error", cancellationToken);
        var snapshot = await browser.BrowserSnapshot(cancellationToken);
        var reference = Regex.Match(snapshot, @"\[ref-\d+\]").Value.Trim('[', ']');
        if (string.IsNullOrEmpty(reference))
        {
            throw new InvalidOperationException(
                "BrowserSnapshot did not expose an actionable reference.");
        }

        await browser.BrowserClick(reference, cancellationToken);

        var staleRejected = false;
        try
        {
            await browser.BrowserClick(reference, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            staleRejected = true;
        }

        if (!staleRejected)
        {
            throw new InvalidOperationException(
                "BrowserClick accepted a stale snapshot reference.");
        }

        var offOriginRejected = false;
        try
        {
            await browser.BrowserNavigate("https://example.com", cancellationToken);
        }
        catch (InvalidOperationException)
        {
            offOriginRejected = true;
        }

        if (!offOriginRejected)
        {
            throw new InvalidOperationException(
                "BrowserNavigate accepted an off-origin URL.");
        }

        VerifyUrlPolicy();
        VerifyImageHistoryTransform();

        return $"{startup} Snapshot/click ready; stale refs rejected; off-origin navigation " +
            $"rejected; console='{console}'; image history scrubbed.";
    }

    private static void VerifyUrlPolicy()
    {
        var policy = new BrowserUrlPolicy(
            "http://127.0.0.1:3001",
            "http://127.0.0.1:4000");

        if (!policy.IsAllowedNavigation("http://127.0.0.1:3001/dashboard") ||
            policy.IsAllowedNavigation("http://127.0.0.1:4000/users") ||
            !policy.IsAllowedRequest("http://127.0.0.1:4000/users") ||
            policy.IsAllowedRequest("http://169.254.169.254/latest/meta-data"))
        {
            throw new InvalidOperationException(
                "Browser URL policy did not enforce the configured application origins.");
        }
    }

    private static void VerifyImageHistoryTransform()
    {
        var oldImage = new DataContent(new byte[] { 1, 2, 3 }, "image/png");
        var latestImage = new DataContent(new byte[] { 4, 5, 6 }, "image/png");
        var transformed = ToolImagePromotingChatClient.PromoteTrailingImages(
        [
            new ChatMessage(
                ChatRole.Tool,
                [new FunctionResultContent("old-screenshot", oldImage)]),
            new ChatMessage(ChatRole.Assistant, "Continue."),
            new ChatMessage(
                ChatRole.Tool,
                [new FunctionResultContent("latest-screenshot", latestImage)]),
        ]);

        var imageStillInToolHistory = transformed
            .Where(message => message.Role == ChatRole.Tool)
            .SelectMany(message => message.Contents)
            .OfType<FunctionResultContent>()
            .Any(result =>
                result.Result is DataContent ||
                result.Result is IEnumerable<AIContent> contents &&
                contents.OfType<DataContent>().Any());
        var promotedImages = transformed
            .Where(message => message.Role == ChatRole.User)
            .SelectMany(message => message.Contents)
            .OfType<DataContent>()
            .Count();

        if (imageStillInToolHistory || promotedImages != 1)
        {
            throw new InvalidOperationException(
                "Tool image history transformation did not scrub old images and promote " +
                "only the latest tool round.");
        }
    }
}
