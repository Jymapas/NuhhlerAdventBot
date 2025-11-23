using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Application.Import;
using FluentAssertions;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using Xunit;

public sealed class ImportParserTests
{
    [Fact]
    public async Task ParseCsvAsync_ShouldParseValidFile()
    {
        const string csv = "date;text\n2025-12-01;hello\n2025-12-02;world\n";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var parser = new ImportParser();

        var rows = await parser.ParseAsync(stream, "test.csv", CancellationToken.None);

        rows.Should().HaveCount(2);
        rows[0].Date.Should().Be(new DateOnly(2025, 12, 1));
        rows[0].Text.Should().Be("hello");
        rows[1].Date.Should().Be(new DateOnly(2025, 12, 2));
        rows[1].Text.Should().Be("world");
    }

    [Fact]
    public async Task ParseExcelAsync_Xlsx_ShouldParseValidFile()
    {
        byte[] bytes;
        using (var workbook = new XSSFWorkbook())
        {
            var sheet = workbook.CreateSheet();
            var header = sheet.CreateRow(0);
            header.CreateCell(0).SetCellValue("date");
            header.CreateCell(1).SetCellValue("text");

            var row1 = sheet.CreateRow(1);
            row1.CreateCell(0).SetCellValue("2025-12-03");
            row1.CreateCell(1).SetCellValue("alpha");

            var row2 = sheet.CreateRow(2);
            row2.CreateCell(0).SetCellValue("2025-12-04");
            row2.CreateCell(1).SetCellValue("beta");

            using var tempStream = new MemoryStream();
            workbook.Write(tempStream);
            bytes = tempStream.ToArray();
        }

        await using var stream = new MemoryStream(bytes);
        var parser = new ImportParser();

        var rows = await parser.ParseAsync(stream, "test.xlsx", CancellationToken.None);

        rows.Should().HaveCount(2);
        rows[0].Date.Should().Be(new DateOnly(2025, 12, 3));
        rows[0].Text.Should().Be("alpha");
        rows[1].Date.Should().Be(new DateOnly(2025, 12, 4));
        rows[1].Text.Should().Be("beta");
    }
}
