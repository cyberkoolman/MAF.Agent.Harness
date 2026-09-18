using System.Collections.Concurrent;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace MAF.RPTeam.Telegram;

internal sealed class TelegramMissionService : IAsyncDisposable
{
    private const int TelegramMessageLimit = 4000;
    private const int MaximumReportLength = 12000;
    private const string HelpText =
        """
        Mission Control commands:
        /mission <request> - start a mission
        /status - show this chat's mission status
        /cancel - cancel this chat's active mission
        /chatid - show this chat's ID for allowlist setup
        /help - show these commands
        """;

    private readonly ITelegramBotClient _bot;
    private readonly HashSet<long> _allowedChatIds;
    private readonly MissionProcessRunner _runner;
    private readonly ConcurrentDictionary<long, MissionExecution> _missions = new();
    private readonly SemaphoreSlim _workspaceLock = new(1, 1);

    public TelegramMissionService(
        ITelegramBotClient bot,
        TelegramSettings settings,
        MissionProcessRunner runner)
    {
        _bot = bot;
        _allowedChatIds = settings.AllowedChatIds.ToHashSet();
        _runner = runner;
    }

    public async Task HandleUpdateAsync(
        ITelegramBotClient botClient,
        Update update,
        CancellationToken cancellationToken)
    {
        if (update.Message is not { Type: MessageType.Text, Text: { } text } message)
        {
            return;
        }

        var chatId = message.Chat.Id;
        var trimmed = text.Trim();
        var (command, argument) = ParseCommand(trimmed);
        if (command == "/chatid")
        {
            await botClient.SendMessage(
                chatId,
                $"This chat ID is {chatId}.",
                cancellationToken: cancellationToken);
            return;
        }

        if (!_allowedChatIds.Contains(chatId))
        {
            Console.Error.WriteLine($"Rejected Telegram chat {chatId}.");
            await botClient.SendMessage(
                chatId,
                "This chat is not authorized. Use /chatid and add that value to Telegram:AllowedChatIds.",
                cancellationToken: cancellationToken);
            return;
        }

        if (command is "/start" or "/help")
        {
            await botClient.SendMessage(chatId, HelpText, cancellationToken: cancellationToken);
            return;
        }

        if (command == "/status")
        {
            var status = _missions.TryGetValue(chatId, out var execution)
                ? execution.Status
                : "No mission is active for this chat.";
            await botClient.SendMessage(chatId, status, cancellationToken: cancellationToken);
            return;
        }

        if (command == "/cancel")
        {
            if (_missions.TryGetValue(chatId, out var execution))
            {
                execution.Cancellation.Cancel();
                await botClient.SendMessage(
                    chatId,
                    "Cancellation requested.",
                    cancellationToken: cancellationToken);
            }
            else
            {
                await botClient.SendMessage(
                    chatId,
                    "No mission is active for this chat.",
                    cancellationToken: cancellationToken);
            }

            return;
        }

        if (command != "/mission")
        {
            await botClient.SendMessage(chatId, HelpText, cancellationToken: cancellationToken);
            return;
        }

        var mission = argument;
        if (mission.Length == 0)
        {
            await botClient.SendMessage(
                chatId,
                "Usage: /mission <request>",
                cancellationToken: cancellationToken);
            return;
        }

        var missionCancellation = new CancellationTokenSource();
        var pending = new MissionExecution(
            missionCancellation,
            $"mission-{Guid.NewGuid():N}");
        if (!_missions.TryAdd(chatId, pending))
        {
            missionCancellation.Dispose();
            await botClient.SendMessage(
                chatId,
                "A mission is already active for this chat. Use /status or /cancel.",
                cancellationToken: cancellationToken);
            return;
        }

        await botClient.SendMessage(
            chatId,
            "Mission accepted. It will start when the shared demo workspace is available.",
            cancellationToken: cancellationToken);

        pending.Task = RunMissionAsync(chatId, mission, pending);
    }

    public Task HandlePollingErrorAsync(
        ITelegramBotClient botClient,
        Exception exception,
        CancellationToken cancellationToken)
    {
        Console.Error.WriteLine($"Telegram polling error: {exception.Message}");
        return Task.CompletedTask;
    }

    private async Task RunMissionAsync(long chatId, string mission, MissionExecution execution)
    {
        try
        {
            execution.Status = "Waiting for the shared demo workspace.";
            await _workspaceLock.WaitAsync(execution.Cancellation.Token);
            try
            {
                execution.Status = "Mission is running.";
                await _bot.SendMessage(
                    chatId,
                    "Mission started.",
                    cancellationToken: execution.Cancellation.Token);

                var result = await _runner.RunAsync(
                    mission,
                    execution.MissionId,
                    chatId,
                    execution.Cancellation.Token);
                execution.Status = result.ExitCode == 0
                    ? "Mission completed successfully."
                    : $"Mission failed with exit code {result.ExitCode}.";

                await SendLongMessageAsync(
                    chatId,
                    FormatMissionResult(execution.Status, result.Output),
                    CancellationToken.None);
            }
            finally
            {
                _workspaceLock.Release();
            }
        }
        catch (OperationCanceledException)
        {
            execution.Status = "Mission cancelled.";
            await _bot.SendMessage(chatId, execution.Status, cancellationToken: CancellationToken.None);
        }
        catch (Exception ex)
        {
            execution.Status = $"Mission failed to run: {ex.Message}";
            await _bot.SendMessage(chatId, execution.Status, cancellationToken: CancellationToken.None);
        }
        finally
        {
            _missions.TryRemove(chatId, out _);
            execution.Cancellation.Dispose();
        }
    }

    private async Task SendLongMessageAsync(
        long chatId,
        string text,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            await _bot.SendMessage(
                chatId,
                "Mission process returned no output.",
                cancellationToken: cancellationToken);
            return;
        }

        for (var offset = 0; offset < text.Length; offset += TelegramMessageLimit)
        {
            var length = Math.Min(TelegramMessageLimit, text.Length - offset);
            await _bot.SendMessage(
                chatId,
                text.Substring(offset, length),
                cancellationToken: cancellationToken);
        }
    }

    private static (string Command, string Argument) ParseCommand(string text)
    {
        var separator = text.IndexOf(' ');
        var token = separator >= 0 ? text[..separator] : text;
        var argument = separator >= 0 ? text[(separator + 1)..].Trim() : string.Empty;
        var mention = token.IndexOf('@');
        var command = mention >= 0 ? token[..mention] : token;
        return (command.ToLowerInvariant(), argument);
    }

    private static string FormatMissionResult(string status, string output)
    {
        const string finalReportMarker = "=== FINAL REPORT ===";
        var marker = output.LastIndexOf(finalReportMarker, StringComparison.Ordinal);
        var report = marker >= 0 ? output[marker..] : output;
        if (report.Length > MaximumReportLength)
        {
            report =
                "[Earlier report text omitted to fit Telegram.]\n" +
                report[^MaximumReportLength..];
        }

        return $"{status}\n\n{report}";
    }

    public async ValueTask DisposeAsync()
    {
        var executions = _missions.Values.ToArray();
        foreach (var execution in executions)
        {
            execution.Cancellation.Cancel();
        }

        await Task.WhenAll(executions.Select(execution => execution.Task));
        _workspaceLock.Dispose();
    }

    private sealed class MissionExecution(
        CancellationTokenSource cancellation,
        string missionId)
    {
        public CancellationTokenSource Cancellation { get; } = cancellation;

        public string MissionId { get; } = missionId;

        public string Status { get; set; } = "Mission accepted.";

        public Task Task { get; set; } = Task.CompletedTask;
    }
}
