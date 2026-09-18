using Microsoft.Extensions.Configuration;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;

namespace MAF.RPTeam.Telegram;

internal sealed class Program
{
    public static async Task<int> Main(string[] args)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddJsonFile("appsettings.Local.json", optional: true)
            .AddUserSecrets<Program>(optional: true)
            .AddEnvironmentVariables("RPTEAM_TELEGRAM_")
            .Build();

        var settings = config.GetSection("Telegram").Get<TelegramSettings>() ?? new();
        var validationError = settings.Validate();
        if (validationError is not null)
        {
            Console.Error.WriteLine(validationError);
            Console.Error.WriteLine(
                "Configure user secrets, appsettings.Local.json, or RPTEAM_TELEGRAM_ variables.");
            return 1;
        }

        string missionProjectPath;
        try
        {
            missionProjectPath = MissionProjectLocator.Resolve(settings.MissionProjectPath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Mission project configuration is invalid: {ex.Message}");
            return 1;
        }

        if (args.Contains("--check-config", StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine("Telegram configuration: valid");
            Console.WriteLine($"  allowed chats   : {settings.AllowedChatIds.Length}");
            Console.WriteLine($"  mission project : {missionProjectPath}");
            return 0;
        }

        using var shutdown = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            shutdown.Cancel();
        };

        TelegramBotClient bot;
        global::Telegram.Bot.Types.User me;
        try
        {
            bot = new TelegramBotClient(settings.BotToken);
            me = await bot.GetMe(shutdown.Token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.Error.WriteLine($"Telegram bot authentication failed: {ex.Message}");
            return 1;
        }

        var runner = new MissionProcessRunner(missionProjectPath);
        var service = new TelegramMissionService(bot, settings, runner);
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = [UpdateType.Message],
            DropPendingUpdates = true,
        };

        Console.WriteLine($"MAF Agent Harness Telegram bot @{me.Username}");
        Console.WriteLine($"  allowed chats   : {settings.AllowedChatIds.Length}");
        Console.WriteLine($"  mission project : {missionProjectPath}");
        Console.WriteLine("  commands        : /mission, /status, /cancel, /chatid, /help");
        if (settings.AllowedChatIds.Length == 0)
        {
            Console.WriteLine("  access          : setup-only; use /chatid, then configure AllowedChatIds");
        }
        Console.WriteLine("Press Ctrl+C to stop.");

        try
        {
            await bot.ReceiveAsync(
                service.HandleUpdateAsync,
                service.HandlePollingErrorAsync,
                receiverOptions,
                shutdown.Token);
            return 0;
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
        {
            return 0;
        }
        finally
        {
            await service.DisposeAsync();
        }
    }
}
