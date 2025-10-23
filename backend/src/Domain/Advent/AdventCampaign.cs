using System;
using System.Collections.Generic;

namespace Domain.Advent;

public enum CampaignStatus
{
    Draft = 0,
    Active = 1,
    Paused = 2,
    Done = 3
}

public enum RecipientStatus
{
    Pending = 0,
    Ready = 1
}

public sealed class AdventCampaign
{
    public long Id { get; set; }
    public long OwnerUserId { get; set; }

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public TimeOnly DefaultSendTime { get; set; }

    public CampaignStatus Status { get; set; } = CampaignStatus.Draft;

    public long? RecipientUserId { get; set; }
    public RecipientStatus RecipientStatus { get; set; } = RecipientStatus.Pending;

    public string Name { get; set; } = "Advent";

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public List<AdventDay> Days { get; set; } = new();
}
