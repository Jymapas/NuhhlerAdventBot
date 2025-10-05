namespace Domain.Entities;

public class User
{
    public long Id { get; set; }
    public string Username { get; set; } = "";
    public string Lang { get; set; } = "ru";
    public DateTime FirstSeenAt { get; set; } = DateTime.UtcNow;
}
