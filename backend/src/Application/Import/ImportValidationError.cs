namespace Application.Import;

public sealed class ImportValidationError
{
    public required string Message { get; init; }
    public required int RowNumber { get; init; }
}
