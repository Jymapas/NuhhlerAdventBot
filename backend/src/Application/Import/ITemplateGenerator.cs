namespace Application.Import;

public interface ITemplateGenerator
{
    Task<(string fileName, string contentType, byte[] data)> GenerateCsvAsync(int year, CancellationToken ct);
    Task<(string fileName, string contentType, byte[] data)> GenerateXlsxAsync(int year, CancellationToken ct);
}
