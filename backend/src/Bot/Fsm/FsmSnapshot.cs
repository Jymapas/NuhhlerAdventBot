namespace Bot.Fsm;

public sealed class FsmSnapshot
{
    public long UserId { get; set; }
    public FsmState State { get; set; }
    public Dictionary<string, string> Payload { get; set; } = new();
    public DateTime UpdatedAtUtc { get; set; }
}
