namespace Application.Import;

/// <summary>
/// Parses CSV/XLS/XLSX file streams into structured rows.
/// </summary>
public interface IImportParser
{
    Task<IReadOnlyList<ImportRow>> ParseAsync(Stream fileStream, string fileName, CancellationToken ct);
}
