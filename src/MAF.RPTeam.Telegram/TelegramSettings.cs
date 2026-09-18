namespace MAF.RPTeam.Telegram;

internal sealed class TelegramSettings
{
    public string BotToken { get; init; } = string.Empty;

    public long[] AllowedChatIds { get; init; } = [];

    public string MissionProjectPath { get; init; } = string.Empty;

    public string? Validate()
    {
        if (string.IsNullOrWhiteSpace(BotToken))
        {
            return "Telegram:BotToken is not configured.";
        }

        return null;
    }
}
