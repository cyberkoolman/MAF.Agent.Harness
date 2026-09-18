using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace MAF.RPTeam.Phases.Phase2.Browser;

/// <summary>
/// Promotes image-valued function results into transient user image content immediately
/// before the provider call. The OpenAI chat-completions adapter supports images in normal
/// messages but flattens tool-role results to strings.
/// </summary>
public sealed class ToolImagePromotingChatClient : DelegatingChatClient
{
    public ToolImagePromotingChatClient(IChatClient innerClient)
        : base(innerClient)
    {
    }

    public override Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        InnerClient.GetResponseAsync(PromoteTrailingImages(messages), options, cancellationToken);

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var update in InnerClient
            .GetStreamingResponseAsync(
                PromoteTrailingImages(messages),
                options,
                cancellationToken)
            .WithCancellation(cancellationToken))
        {
            yield return update;
        }
    }

    internal static IReadOnlyList<ChatMessage> PromoteTrailingImages(
        IEnumerable<ChatMessage> messages)
    {
        var transformed = messages.ToList();
        var firstTrailingTool = transformed.Count;
        while (firstTrailingTool > 0 &&
               transformed[firstTrailingTool - 1].Role == ChatRole.Tool)
        {
            firstTrailingTool--;
        }

        var promotedImages = new List<AIContent>();
        var promotedCallIds = new List<string>();

        for (var index = 0; index < transformed.Count; index++)
        {
            var original = transformed[index];
            var replacementContents = new List<AIContent>(original.Contents.Count);
            var changed = false;

            foreach (var content in original.Contents)
            {
                if (content is FunctionResultContent result &&
                    ContainsImages(result.Result))
                {
                    changed = true;
                    if (index >= firstTrailingTool)
                    {
                        ExtractImages(result.Result, promotedImages);
                        promotedCallIds.Add(result.CallId);
                    }

                    replacementContents.Add(new FunctionResultContent(
                        result.CallId,
                        index >= firstTrailingTool
                            ? "Browser screenshot captured as image/png and attached in the next message."
                            : "Browser screenshot from an earlier tool round was omitted from history."));
                }
                else
                {
                    replacementContents.Add(content);
                }
            }

            if (changed)
            {
                var clone = original.Clone();
                clone.Contents = replacementContents;
                transformed[index] = clone;
            }
        }

        if (promotedImages.Count == 0)
        {
            return transformed;
        }

        var userContents = new List<AIContent>
        {
            new TextContent(
                "Image output from BrowserScreenshot. Analyze the attached image as the " +
                $"result of tool call(s): {string.Join(", ", promotedCallIds.Distinct())}."),
        };
        userContents.AddRange(promotedImages);
        transformed.Add(new ChatMessage(ChatRole.User, userContents));

        return transformed;
    }

    private static bool ContainsImages(object? value)
    {
        return value is DataContent data && IsImage(data) ||
               value is IEnumerable<AIContent> contents &&
               contents.OfType<DataContent>().Any(IsImage);
    }

    private static void ExtractImages(
        object? value,
        ICollection<AIContent> destination)
    {
        if (value is DataContent data && IsImage(data))
        {
            destination.Add(data);
            return;
        }

        if (value is IEnumerable<AIContent> contents)
        {
            foreach (var image in contents.OfType<DataContent>().Where(IsImage))
            {
                destination.Add(image);
            }
        }
    }

    private static bool IsImage(DataContent content) =>
        content.MediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
}
