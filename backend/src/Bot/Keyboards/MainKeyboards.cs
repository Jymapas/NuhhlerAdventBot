using Telegram.Bot.Types.ReplyMarkups;

namespace Bot.Keyboards;

public static class MainKeyboards
{
    public static InlineKeyboardMarkup CreateMainMenu() =>
        new(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("Check", "cmd:/check"),
                InlineKeyboardButton.WithCallbackData("Import", "cmd:/import")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("Start", "cmd:/start_campaign"),
                InlineKeyboardButton.WithCallbackData("Today", "cmd:/today")
            }
        });

    public static InlineKeyboardMarkup CreateCancel() =>
        new(InlineKeyboardButton.WithCallbackData("Отмена", "cmd:/cancel"));
}
