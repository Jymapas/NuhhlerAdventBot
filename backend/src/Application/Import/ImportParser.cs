using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CsvHelper;
using CsvHelper.Configuration;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace Application.Import;

public sealed class ImportParser : IImportParser
{
    public async Task<IReadOnlyList<ImportRow>> ParseAsync(Stream fileStream, string fileName, CancellationToken ct)
    {
        var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
        return extension switch
        {
            ".csv"  => await ParseCsvAsync(fileStream, ct),
            ".xlsx" => ParseExcel(fileStream, isXlsx: true),
            ".xls"  => ParseExcel(fileStream, isXlsx: false),
            _       => throw new InvalidOperationException("Неподдерживаемый формат. Пришлите CSV, XLS или XLSX.")
        };
    }

    private static async Task<IReadOnlyList<ImportRow>> ParseCsvAsync(Stream fileStream, CancellationToken ct)
    {
        fileStream.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(fileStream, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            TrimOptions = TrimOptions.Trim,
            MissingFieldFound = null,
            BadDataFound = null,
            Delimiter = ";"
        });

        var rows = new List<ImportRow>();
        while (await csv.ReadAsync())
        {
            ct.ThrowIfCancellationRequested();

            if (csv.Context.Record is null || csv.Context.Record.Length == 0)
            {
                continue;
            }

            var dateStr = csv.GetField("date");
            var textStr = csv.GetField("text");

            if (string.IsNullOrWhiteSpace(dateStr) && string.IsNullOrWhiteSpace(textStr))
            {
                continue;
            }

            if (!DateOnly.TryParse(dateStr, CultureInfo.InvariantCulture, out var dateValue))
            {
                throw new InvalidOperationException($"Некорректная дата '{dateStr}'.");
            }

            rows.Add(new ImportRow
            {
                Date = dateValue,
                Text = textStr?.Trim() ?? string.Empty
            });
        }

        return rows;
    }

    private static IReadOnlyList<ImportRow> ParseExcel(Stream fileStream, bool isXlsx)
    {
        fileStream.Seek(0, SeekOrigin.Begin);
        IWorkbook workbook = isXlsx
            ? new XSSFWorkbook(fileStream)
            : new HSSFWorkbook(fileStream);

        var sheet = workbook.GetSheetAt(0);
        var rows = new List<ImportRow>();

        for (var i = 1; i <= sheet.LastRowNum; i++)
        {
            var row = sheet.GetRow(i);
            if (row is null)
            {
                continue;
            }

            var dateCell = row.GetCell(0);
            var textCell = row.GetCell(1);

            var dateString = ExtractCellString(dateCell);
            var textString = ExtractCellString(textCell);

            if (string.IsNullOrWhiteSpace(dateString) && string.IsNullOrWhiteSpace(textString))
            {
                continue;
            }

            if (!DateOnly.TryParse(dateString, CultureInfo.InvariantCulture, out var dateValue))
            {
                throw new InvalidOperationException($"Некорректная дата '{dateString}' в строке {i + 1}.");
            }

            rows.Add(new ImportRow
            {
                Date = dateValue,
                Text = textString
            });
        }

        return rows;
    }

    private static string ExtractCellString(ICell? cell)
    {
        if (cell is null)
        {
            return string.Empty;
        }

        return cell.CellType switch
        {
            CellType.Numeric when DateUtil.IsCellDateFormatted(cell) =>
                DateOnly.FromDateTime(cell.DateCellValue).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            CellType.Numeric => cell.NumericCellValue.ToString(CultureInfo.InvariantCulture),
            CellType.Boolean => cell.BooleanCellValue ? "true" : "false",
            CellType.String => cell.StringCellValue.Trim(),
            CellType.Formula => ExtractCellString(EvaluateFormulaCell(cell)),
            _ => cell.ToString()?.Trim() ?? string.Empty
        };
    }

    private static ICell? EvaluateFormulaCell(ICell cell)
    {
        var evaluator = cell.Sheet.Workbook.GetCreationHelper().CreateFormulaEvaluator();
        var evaluated = evaluator.EvaluateInCell(cell);
        return evaluated;
    }
}
