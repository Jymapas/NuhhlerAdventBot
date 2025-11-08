using System;
using FluentAssertions;
using Shared.Logging;
using Xunit;

public class GlobalExceptionWireupTests
{
    [Fact]
    public void RegisterDoesNotThrow()
    {
        Action act = () => GlobalExceptionHandler.Register("Test");
        act.Should().NotThrow();
    }
}
