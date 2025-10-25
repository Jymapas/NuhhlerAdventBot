using System;
using System.Collections.Generic;
using Telegram.Bot.Types.ReplyMarkups;

namespace Bot.Keyboards;

public static class CheckKeyboards
{
    private static readonly (int Start, int End)[] Ranges =
    {
        (1, 10),
        (11, 20),
        (21, 31)
    };

    public static InlineKeyboardMarkup BuildRangeKeyboard()
    {
        var buttons = new List<InlineKeyboardButton>();
        foreach (var (start, end) in Ranges)
        {
            var text = $"{start}–{end}";
            buttons.Add(InlineKeyboardButton.WithCallbackData(text, $"check:range:{start}-{end}"));
        }

        return new InlineKeyboardMarkup(buttons);
    }

    public static InlineKeyboardMarkup BuildDaysKeyboard(int rangeStart, int rangeEnd)
    {
        var rows = new List<IEnumerable<InlineKeyboardButton>>();
        var currentRow = new List<InlineKeyboardButton>();

        for (var day = rangeStart; day <= rangeEnd; day++)
        {
            var text = day.ToString("00");
            currentRow.Add(InlineKeyboardButton.WithCallbackData(text, $"check:day:{text}"));

            if (currentRow.Count == 5)
            {
                rows.Add(currentRow);
                currentRow = new List<InlineKeyboardButton>();
            }
        }

        if (currentRow.Count > 0)
        {
            rows.Add(currentRow);
        }

        rows.Add(new[]
        {
            InlineKeyboardButton.WithCallbackData("⬅ Назад", "check:back:ranges")
        });

        return new InlineKeyboardMarkup(rows);
    }

    public static InlineKeyboardMarkup BuildDayActionsKeyboard(string dateIso)
    {
        var rangeId = DetermineRangeId(DateOnly.Parse(dateIso));

        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("✏ Текст", $"edit:text:{dateIso}"),
                InlineKeyboardButton.WithCallbackData("⏰ Время", $"edit:time:{dateIso}")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("⬅ Назад", $"check:back:days:{rangeId}")
            }
        });
    }

    private static string DetermineRangeId(DateOnly date)
    {
        var day = date.Day;
        if (day <= 10) return "1-10";
        if (day <= 20) return "11-20";
        return "21-31";
    }
}
