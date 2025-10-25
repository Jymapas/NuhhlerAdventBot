using System.Linq;
using Bot.Keyboards;
using FluentAssertions;
using Telegram.Bot.Types.ReplyMarkups;
using Xunit;

public class CheckKeyboardTests
{
    [Fact]
    public void RangeKeyboardContainsThreeButtons()
    {
        var keyboard = CheckKeyboards.BuildRangeKeyboard();
        keyboard.InlineKeyboard.Should().NotBeNull();

        var buttons = keyboard.InlineKeyboard!
            .SelectMany(row => row)
            .ToList();

        buttons.Should().HaveCount(3);
        buttons.Select(b => b.CallbackData).Should().Contain(new[]
        {
            "check:range:1-10",
            "check:range:11-20",
            "check:range:21-31"
        });
    }

    [Fact]
    public void DaysKeyboardContainsDaysAndBackButton()
    {
        var keyboard = CheckKeyboards.BuildDaysKeyboard(1, 10);
        var rows = keyboard.InlineKeyboard!.ToList();

        rows.Should().NotBeEmpty();

        var dayButtons = rows
            .Take(rows.Count - 1)
            .SelectMany(r => r)
            .Select(b => b.CallbackData)
            .ToList();

        dayButtons.Should().Contain("check:day:01");
        dayButtons.Should().Contain("check:day:10");
        dayButtons.Should().HaveCount(10);

        var backRow = rows.Last();
        backRow.Should().ContainSingle();
        backRow.Single().CallbackData.Should().Be("check:back:ranges");
    }
}
