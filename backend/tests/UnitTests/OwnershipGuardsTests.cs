using Bot.Security;
using Domain.Advent;
using Domain.Users;
using FluentAssertions;
using Xunit;

public class OwnershipGuardsTests
{
    [Fact]
    public void IsCampaignOwnerReturnsTrueForMatchingOwner()
    {
        var owner = new User { Id = 42, TelegramId = 12345 };
        var campaign = new AdventCampaign { OwnerUserId = owner.Id };

        OwnershipGuards.IsCampaignOwner(campaign, owner.Id).Should().BeTrue();
    }

    [Fact]
    public void IsCampaignOwnerReturnsFalseForDifferentOwner()
    {
        var campaign = new AdventCampaign { OwnerUserId = 42 };

        OwnershipGuards.IsCampaignOwner(campaign, 99).Should().BeFalse();
    }

    [Fact]
    public void IsCampaignOwnerReturnsFalseForMissingCampaign()
    {
        OwnershipGuards.IsCampaignOwner(null, 42).Should().BeFalse();
    }
}
