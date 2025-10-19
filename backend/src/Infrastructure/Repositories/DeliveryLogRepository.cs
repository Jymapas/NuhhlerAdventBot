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

    public async Task LogAsync(DeliveryLog log, CancellationToken ct)
    {
        await dbContext.DeliveryLogs.AddAsync(log, ct);
        await dbContext.SaveChangesAsync(ct);
    }
}
