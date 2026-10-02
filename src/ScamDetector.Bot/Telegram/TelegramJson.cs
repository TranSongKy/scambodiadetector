using System.Text.Json;

namespace ScamDetector.Bot.Telegram;

public static class TelegramJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };
}
