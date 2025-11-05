using System.Collections.Generic;
using Bot.Security;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

public class AuthExtensionsTests
{
    [Fact]
    public void ReturnsTrueWhenOwnerIdsMissing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        AuthExtensions.IsAllowedOwner(123, configuration).Should().BeTrue();
        AuthExtensions.IsAllowedOwner(456, configuration).Should().BeTrue();
    }

    [Fact]
    public void RespectsConfiguredOwnerIds()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OWNER_IDS"] = "111, 222 ,333"
            })
            .Build();

        AuthExtensions.IsAllowedOwner(111, configuration).Should().BeTrue();
        AuthExtensions.IsAllowedOwner(333, configuration).Should().BeTrue();
        AuthExtensions.IsAllowedOwner(999, configuration).Should().BeFalse();
    }
}
