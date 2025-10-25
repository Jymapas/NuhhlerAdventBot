namespace Application.Import;

public interface IImportService
{
    Task<ImportResult> ImportIntoCampaignAsync(
        long ownerUserId,
        long campaignId,
        IReadOnlyList<ImportRow> rows,
        CancellationToken ct);
}

public sealed class ImportResult
{
    public required int CreatedDays { get; init; }
    public required int UpdatedDays { get; init; }
    public required IReadOnlyList<ImportValidationError> Errors { get; init; }
}
