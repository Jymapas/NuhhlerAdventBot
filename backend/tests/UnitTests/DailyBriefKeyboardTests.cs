using System.Linq;
using Bot.Keyboards;
using FluentAssertions;
using Telegram.Bot.Types.ReplyMarkups;
using Xunit;

public class DailyBriefKeyboardTests
{
    [Fact]
    public void BuildDailyBriefKeyboardContainsRequiredButtons()
    {
        var keyboard = DailyBriefKeyboards.BuildDailyBriefKeyboard(123, "2025-12-15");
        keyboard.Should().NotBeNull();

        var callbacks = keyboard
            .InlineKeyboard!
            .SelectMany(row => row)
            .Select(button => button.CallbackData)
            .ToList();

        callbacks.Should().Contain("brief:sendnow:123:2025-12-15");
        callbacks.Should().Contain("brief:pause:123:2025-12-15");
        callbacks.Should().Contain("edit:text:2025-12-15");
        callbacks.Should().Contain("edit:time:2025-12-15");
    }
}
