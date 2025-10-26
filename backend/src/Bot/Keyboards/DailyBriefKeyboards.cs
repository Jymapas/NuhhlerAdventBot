using Telegram.Bot.Types.ReplyMarkups;

namespace Bot.Keyboards;

public static class DailyBriefKeyboards
{
    public static InlineKeyboardMarkup BuildDailyBriefKeyboard(long campaignId, string dateIso)
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("Отправить сейчас", $"brief:sendnow:{campaignId}:{dateIso}") },
            new[] { InlineKeyboardButton.WithCallbackData("Пауза до завтра", $"brief:pause:{campaignId}:{dateIso}") },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("✏ Текст", $"edit:text:{dateIso}"),
                InlineKeyboardButton.WithCallbackData("⏰ Время", $"edit:time:{dateIso}")
            }
        });
    }
}
