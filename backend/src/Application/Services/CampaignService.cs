using System;
using System.Linq;
using Application.Abstractions;
using Domain.Advent;

namespace Application.Services;

public sealed class CampaignService
{
    private readonly IAdventRepository _adventRepository;

    public CampaignService(IAdventRepository adventRepository)
    {
        _adventRepository = adventRepository;
    }

    public Task<AdventCampaign> CreateDefaultDecemberCampaignAsync(long ownerUserId, CancellationToken ct) =>
        CreateDefaultDecemberCampaignAsync(ownerUserId, DateTime.UtcNow.Year, ct);

    public async Task<AdventCampaign> CreateDefaultDecemberCampaignAsync(long ownerUserId, int year, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var existing = await _adventRepository.GetDraftByOwnerAsync(ownerUserId, ct);
        if (existing is not null)
        {
            throw new InvalidOperationException($"Draft campaign already exists (ID = {existing.Id}).");
        }

        var start = new DateOnly(year, 12, 1);
        var end = new DateOnly(year, 12, 31);

        var campaign = new AdventCampaign
        {
            OwnerUserId = ownerUserId,
            Name = $"Advent-{year}",
            StartDate = start,
            EndDate = end,
            DefaultSendTime = new TimeOnly(9, 0),
            Status = CampaignStatus.Draft,
            RecipientStatus = RecipientStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        var days = Enumerable.Range(0, end.Day - start.Day + 1)
            .Select(i => new AdventDay
            {
                Date = start.AddDays(i),
                Text = string.Empty
            })
            .ToArray();

        await _adventRepository.CreateCampaignAsync(campaign, days, ct);

        return await _adventRepository.GetCampaignAsync(campaign.Id, ct)
            ?? throw new InvalidOperationException("Unable to load campaign after creation.");
    }

    public async Task<AdventCampaign> AssignRecipientAsync(long campaignId, long recipientUserId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var campaign = await _adventRepository.GetCampaignAsync(campaignId, ct)
            ?? throw new InvalidOperationException($"Campaign {campaignId} not found.");

        campaign.RecipientUserId = recipientUserId;
        campaign.RecipientStatus = RecipientStatus.Ready;
        if (campaign.Status == CampaignStatus.Draft)
        {
            campaign.Status = CampaignStatus.Draft;
        }

        campaign.BindToken = null;
        campaign.UpdatedAtUtc = DateTime.UtcNow;

        await _adventRepository.UpdateCampaignAsync(campaign, ct);

        return await _adventRepository.GetCampaignAsync(campaignId, ct)
            ?? throw new InvalidOperationException($"Campaign {campaignId} not found after update.");
    }
}
