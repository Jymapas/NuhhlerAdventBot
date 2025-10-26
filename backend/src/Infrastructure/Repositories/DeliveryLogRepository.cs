using System;
using System.Linq;
using Application.Abstractions;
using Domain.Delivery;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public sealed class DeliveryLogRepository(AppDbContext dbContext) : IDeliveryLogRepository
{
    public async Task<bool> IsSentAsync(long campaignId, long recipientUserId, DateOnly date, CancellationToken ct) =>
        await dbContext.DeliveryLogs.AnyAsync(
            x => x.CampaignId == campaignId
                 && x.RecipientUserId == recipientUserId
                 && x.Date == date
                 && x.Status == DeliveryStatus.Sent,
            ct);

    public async Task LogAsync(DeliveryLog log, CancellationToken ct) => await SaveAsync(log, ct);

    public async Task<IReadOnlyList<DeliveryLog>> GetRetryCandidatesAsync(CancellationToken ct) =>
        await dbContext.DeliveryLogs
            .Where(x => x.Status == DeliveryStatus.Retry)
            .ToListAsync(ct);

    public async Task SaveAsync(DeliveryLog log, CancellationToken ct)
    {
        if (log.CreatedAtUtc == default)
        {
            log.CreatedAtUtc = DateTime.UtcNow;
        }

        if (log.LastAttemptAtUtc == default)
        {
            log.LastAttemptAtUtc = DateTime.UtcNow;
        }

        var entry = dbContext.Entry(log);
        if (entry.State == EntityState.Detached)
        {
            if (log.Id == 0)
            {
                await dbContext.DeliveryLogs.AddAsync(log, ct);
            }
            else
            {
                dbContext.DeliveryLogs.Update(log);
            }
        }

        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<DeliveryLog?> GetLogAsync(long campaignId, long recipientUserId, DateOnly date, CancellationToken ct) =>
        await dbContext.DeliveryLogs
            .Where(x => x.CampaignId == campaignId && x.RecipientUserId == recipientUserId && x.Date == date)
            .OrderByDescending(x => x.LastAttemptAtUtc)
            .FirstOrDefaultAsync(ct);
}
