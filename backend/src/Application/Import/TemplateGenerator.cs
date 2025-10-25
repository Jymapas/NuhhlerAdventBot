using System.Globalization;
using System.Text;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace Application.Import;

public sealed class TemplateGenerator : ITemplateGenerator
{
    public Task<(string fileName, string contentType, byte[] data)> GenerateCsvAsync(int year, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var builder = new StringBuilder();
        builder.AppendLine("date;text");

        for (var day = 1; day <= 31; day++)
        {
            var date = new DateOnly(year, 12, day);
            builder.Append(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            builder.Append(';');
            builder.AppendLine($"текст сообщения на день {day}");
        }

        var bytes = Encoding.UTF8.GetBytes(builder.ToString());
        return Task.FromResult((
            $"advent-template-{year}.csv",
            "text/csv; charset=utf-8",
            bytes));
    }

    public Task<(string fileName, string contentType, byte[] data)> GenerateXlsxAsync(int year, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        using var workbook = new XSSFWorkbook();
        var sheet = workbook.CreateSheet("advent");

        var header = sheet.CreateRow(0);
        header.CreateCell(0).SetCellValue("date");
        header.CreateCell(1).SetCellValue("text");

        for (var day = 1; day <= 31; day++)
        {
            var row = sheet.CreateRow(day);
            var date = new DateOnly(year, 12, day);
            row.CreateCell(0).SetCellValue(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            row.CreateCell(1).SetCellValue($"текст сообщения на день {day}");
        }

        using var stream = new MemoryStream();
        workbook.Write(stream, leaveOpen: true);
        var bytes = stream.ToArray();

        return Task.FromResult((
            $"advent-template-{year}.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            bytes));
    }
}
