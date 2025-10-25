namespace Application.Import;

public sealed class ImportRow
{
    public DateOnly Date { get; init; }
    public string Text { get; init; } = string.Empty;
}
