using System;

namespace Domain.Delivery;

public enum DeliveryStatus
{
    Sent = 0,
    Retry = 1,
    Failed = 2
}

public sealed class DeliveryLog
{
    public long Id { get; set; }
    public long CampaignId { get; set; }
    public long RecipientUserId { get; set; }

    public DateOnly Date { get; set; }
    public DeliveryStatus Status { get; set; }

    public int Attempts { get; set; }
    public string? Error { get; set; }
    public int? TelegramMessageId { get; set; }
    public DateTime? SentAtUtc { get; set; }
}
