using System;
using FluentAssertions;
using Worker.DailyBrief;
using Xunit;

public class DailyBriefTimeCalcTests
{
    [Fact]
    public void ShouldSendBriefAtEightWhenNotSent()
    {
        DailyBriefService.ShouldSendBrief(new DateTime(2025, 12, 1, 8, 0, 0), null).Should().BeTrue();
    }

    [Fact]
    public void ShouldNotSendBriefTwiceSameDay()
    {
        var today = new DateOnly(2025, 12, 1);
        DailyBriefService.ShouldSendBrief(new DateTime(2025, 12, 1, 8, 0, 0), today).Should().BeFalse();
    }

    [Fact]
    public void ShouldNotSendBeforeEight()
    {
        DailyBriefService.ShouldSendBrief(new DateTime(2025, 12, 1, 7, 59, 0), null).Should().BeFalse();
    }

    [Fact]
    public void ShouldNotSendAfterEightMinute()
    {
        DailyBriefService.ShouldSendBrief(new DateTime(2025, 12, 1, 8, 1, 0), null).Should().BeFalse();
    }
}
