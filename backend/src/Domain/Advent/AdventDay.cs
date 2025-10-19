using System;

namespace Domain.Advent;

public sealed class AdventDay
{
    public long Id { get; set; }
    public long CampaignId { get; set; }

    public DateOnly Date { get; set; }
    public string Text { get; set; } = string.Empty;

    public TimeOnly? OverrideSendTime { get; set; }
}
