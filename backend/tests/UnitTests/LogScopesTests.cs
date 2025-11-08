using System;
using FluentAssertions;
using Shared.Logging;
using Xunit;

public class LogScopesTests
{
    [Fact]
    public void WithCampaignReturnsDisposable()
    {
        using var scope = LogScopes.WithCampaign(123, "2025-12-24");
        scope.Should().NotBeNull();
    }

    [Fact]
    public void WithUpdateReturnsDisposable()
    {
        using var scope = LogScopes.WithUpdate(555, 777, "alex");
        scope.Should().NotBeNull();
    }
}
