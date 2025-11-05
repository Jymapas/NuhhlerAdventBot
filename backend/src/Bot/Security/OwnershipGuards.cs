using Domain.Advent;

namespace Bot.Security;

public static class OwnershipGuards
{
    public static bool IsCampaignOwner(AdventCampaign? campaign, long ownerUserId) =>
        campaign is not null && campaign.OwnerUserId == ownerUserId;
}
