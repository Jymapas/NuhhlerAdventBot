namespace Domain.Entities;

public class Note
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string Username { get; set; } = "";
    public string Text { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
