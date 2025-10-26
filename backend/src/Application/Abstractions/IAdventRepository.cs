using Domain.Advent;

namespace Application.Abstractions;

public interface IAdventRepository
{
    Task<AdventCampaign?> GetCampaignAsync(long campaignId, CancellationToken ct);
    Task<long> CreateCampaignAsync(AdventCampaign campaign, IEnumerable<AdventDay> days, CancellationToken ct);
    Task UpdateCampaignAsync(AdventCampaign campaign, CancellationToken ct);

    Task<AdventDay?> GetDayAsync(long campaignId, DateOnly date, CancellationToken ct);
    Task UpsertDayAsync(AdventDay day, CancellationToken ct);

    Task<bool> CampaignHasAllDaysFilledAsync(long campaignId, CancellationToken ct);
    Task<AdventCampaign?> GetDraftByOwnerAsync(long ownerUserId, CancellationToken ct);
    Task<AdventCampaign?> GetByBindTokenAsync(string token, CancellationToken ct);
    Task<AdventCampaign?> GetActiveByOwnerAsync(long ownerUserId, CancellationToken ct);
    Task<AdventCampaign?> GetActiveOrDraftByOwnerAsync(long ownerUserId, CancellationToken ct);
    Task<IReadOnlyList<AdventCampaign>> GetActiveCampaignsAsync(CancellationToken ct);
    Task<IReadOnlyList<AdventCampaign>> GetCampaignsForDailyBriefAsync(CancellationToken ct);
}
