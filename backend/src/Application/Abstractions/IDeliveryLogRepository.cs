using Domain.Delivery;

namespace Application.Abstractions;

public interface IDeliveryLogRepository
{
    Task<bool> IsSentAsync(long campaignId, long recipientUserId, DateOnly date, CancellationToken ct);
    Task LogAsync(DeliveryLog log, CancellationToken ct);
}
