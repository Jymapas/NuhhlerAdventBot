using System;
using Application.Services;
using Domain.Delivery;
using FluentAssertions;
using Xunit;

public class DeliveryRetryLogicTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 5)]
    [InlineData(3, 15)]
    [InlineData(4, 30)]
    [InlineData(5, null)]
    public void RetryDelaysMatchSpecification(int attempts, int? expectedMinutes)
    {
        var delay = DeliveryService.GetRetryDelayByAttempts(attempts);
        if (expectedMinutes is null)
        {
            delay.Should().BeNull();
        }
        else
        {
            delay.Should().NotBeNull();
            delay!.Value.TotalMinutes.Should().Be(expectedMinutes.Value);
        }
    }

    [Fact]
    public void RetryDueAfterRequiredDelay()
    {
        var log = new DeliveryLog
        {
            Attempts = 1,
            LastAttemptAtUtc = DateTime.UtcNow.AddMinutes(-2)
        };

        DeliveryService.IsRetryDue(log, DateTime.UtcNow).Should().BeTrue();
    }

    [Fact]
    public void RetryNotDueBeforeDelay()
    {
        var log = new DeliveryLog
        {
            Attempts = 1,
            LastAttemptAtUtc = DateTime.UtcNow.AddSeconds(-30)
        };

        DeliveryService.IsRetryDue(log, DateTime.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void RetryNotDueWhenAttemptsExceeded()
    {
        var log = new DeliveryLog
        {
            Attempts = DeliveryService.MaxAttempts,
            LastAttemptAtUtc = DateTime.UtcNow.AddHours(-1)
        };

        DeliveryService.IsRetryDue(log, DateTime.UtcNow).Should().BeFalse();
    }
}
