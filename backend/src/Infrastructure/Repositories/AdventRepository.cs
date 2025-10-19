using System;
using System.Collections.Generic;
using System.Linq;
using Application.Abstractions;
using Domain.Advent;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public sealed class AdventRepository(AppDbContext dbContext) : IAdventRepository
{
    public async Task<AdventCampaign?> GetCampaignAsync(long campaignId, CancellationToken ct) =>
        await dbContext.AdventCampaigns
            .Include(c => c.Days)
            .FirstOrDefaultAsync(c => c.Id == campaignId, ct);

    public async Task<long> CreateCampaignAsync(AdventCampaign campaign, IEnumerable<AdventDay> days, CancellationToken ct)
    {
        campaign.CreatedAtUtc = DateTime.UtcNow;
        campaign.UpdatedAtUtc = DateTime.UtcNow;

        await dbContext.AdventCampaigns.AddAsync(campaign, ct);
        await dbContext.SaveChangesAsync(ct);

        foreach (var day in days)
        {
            day.CampaignId = campaign.Id;
        }

        await dbContext.AdventDays.AddRangeAsync(days, ct);
        await dbContext.SaveChangesAsync(ct);

        return campaign.Id;
    }

    public async Task UpdateCampaignAsync(AdventCampaign campaign, CancellationToken ct)
    {
        campaign.UpdatedAtUtc = DateTime.UtcNow;
        dbContext.AdventCampaigns.Update(campaign);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<AdventDay?> GetDayAsync(long campaignId, DateOnly date, CancellationToken ct) =>
        await dbContext.AdventDays.FirstOrDefaultAsync(
            x => x.CampaignId == campaignId && x.Date == date,
            ct);

    public async Task UpsertDayAsync(AdventDay day, CancellationToken ct)
    {
        var existing = await dbContext.AdventDays.FirstOrDefaultAsync(
            x => x.CampaignId == day.CampaignId && x.Date == day.Date,
            ct);

        if (existing is null)
        {
            await dbContext.AdventDays.AddAsync(day, ct);
        }
        else
        {
            existing.Text = day.Text;
            existing.OverrideSendTime = day.OverrideSendTime;
            dbContext.AdventDays.Update(existing);
        }

        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<bool> CampaignHasAllDaysFilledAsync(long campaignId, CancellationToken ct)
    {
        var query = dbContext.AdventDays.AsQueryable().Where(x => x.CampaignId == campaignId);
        var total = await query.CountAsync(ct);
        if (total == 0)
        {
            return false;
        }

        var empty = await query.CountAsync(x => string.IsNullOrWhiteSpace(x.Text), ct);
        return empty == 0;
    }
}
