using Application.Abstractions;
using Domain.Advent;

namespace Application.Services;

public sealed class TimeSetupService
{
    private readonly IAdventRepository _adventRepository;

    public TimeSetupService(IAdventRepository adventRepository)
    {
        _adventRepository = adventRepository;
    }

    public async Task<(bool ok, string? error)> SetCampaignTimeAsync(long ownerUserId, TimeOnly time, CancellationToken ct)
    {
        var campaign = await _adventRepository.GetActiveOrDraftByOwnerAsync(ownerUserId, ct);
        if (campaign is null)
        {
            return (false, "Нет активной кампании.");
        }

        campaign.DefaultSendTime = time;
        await _adventRepository.UpdateCampaignAsync(campaign, ct);
        return (true, null);
    }

    public async Task<(bool ok, string? error)> SetDayTimeAsync(long ownerUserId, DateOnly date, TimeOnly time, CancellationToken ct)
    {
        var campaign = await _adventRepository.GetActiveOrDraftByOwnerAsync(ownerUserId, ct);
        if (campaign is null)
        {
            return (false, "Нет активной кампании.");
        }

        if (date < campaign.StartDate || date > campaign.EndDate)
        {
            return (false, "Дата вне диапазона кампании.");
        }

        var day = await _adventRepository.GetDayAsync(campaign.Id, date, ct);
        if (day is null)
        {
            return (false, "День не найден.");
        }

        day.OverrideSendTime = time;
        await _adventRepository.UpsertDayAsync(day, ct);
        await _adventRepository.UpdateCampaignAsync(campaign, ct);
        return (true, null);
    }

    public async Task<(bool ok, string? error)> ResetDayTimeAsync(long ownerUserId, DateOnly date, CancellationToken ct)
    {
        var campaign = await _adventRepository.GetActiveOrDraftByOwnerAsync(ownerUserId, ct);
        if (campaign is null)
        {
            return (false, "Нет активной кампании.");
        }

        if (date < campaign.StartDate || date > campaign.EndDate)
        {
            return (false, "Дата вне диапазона кампании.");
        }

        var day = await _adventRepository.GetDayAsync(campaign.Id, date, ct);
        if (day is null)
        {
            return (false, "День не найден.");
        }

        day.OverrideSendTime = null;
        await _adventRepository.UpsertDayAsync(day, ct);
        await _adventRepository.UpdateCampaignAsync(campaign, ct);
        return (true, null);
    }
}
