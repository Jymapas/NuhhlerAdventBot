using Application;
using FluentAssertions;
using Xunit;

public class SmokeTests
{
    [Fact]
    public void BotInfo_Version_IsSet() => new BotInfo().Version.Should().StartWith("PoC");
}
