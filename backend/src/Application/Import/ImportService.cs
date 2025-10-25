using System;
using System.Collections.Generic;
using Application.Abstractions;
using Domain.Advent;

namespace Application.Import;

public sealed class ImportService(IAdventRepository adventRepository) : IImportService
{
    public async Task<ImportResult> ImportIntoCampaignAsync(
        long ownerUserId,
        long campaignId,
        IReadOnlyList<ImportRow> rows,
        CancellationToken ct)
    {
        var campaign = await adventRepository.GetCampaignAsync(campaignId, ct);
        if (campaign is null || campaign.OwnerUserId != ownerUserId)
        {
            return new ImportResult
            {
                CreatedDays = 0,
                UpdatedDays = 0,
                Errors = new[]
                {
                    new ImportValidationError
                    {
                        RowNumber = 0,
                        Message = "Кампания не найдена или не принадлежит вам."
                    }
                }
            };
        }

        var year = campaign.StartDate.Year;
        var start = campaign.StartDate;
        var end = campaign.EndDate;

        var errors = new List<ImportValidationError>();
        var seenDates = new HashSet<DateOnly>();
        var validRows = new List<(ImportRow Row, int RowNumber)>();

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var rowNumber = i + 2; // header is row 1

            if (string.IsNullOrWhiteSpace(row.Text))
            {
                errors.Add(new ImportValidationError { RowNumber = rowNumber, Message = "Текст пустой." });
                continue;
            }

            if (row.Text.Trim().Length == 0)
            {
                errors.Add(new ImportValidationError { RowNumber = rowNumber, Message = "Текст пустой." });
                continue;
            }

            if (row.Text.Length > 4096)
            {
                errors.Add(new ImportValidationError { RowNumber = rowNumber, Message = "Текст длиннее 4096 символов." });
                continue;
            }

            if (row.Date.Year != year || row.Date < start || row.Date > end)
            {
                errors.Add(new ImportValidationError { RowNumber = rowNumber, Message = "Дата вне диапазона кампании." });
                continue;
            }

            if (!seenDates.Add(row.Date))
            {
                errors.Add(new ImportValidationError { RowNumber = rowNumber, Message = "Повторяющаяся дата в файле." });
                continue;
            }

            validRows.Add((row, rowNumber));
        }

        if (errors.Count > 0)
        {
            return new ImportResult
            {
                CreatedDays = 0,
                UpdatedDays = 0,
                Errors = errors
            };
        }

        var created = 0;
        var updated = 0;

        foreach (var (row, _) in validRows)
        {
            ct.ThrowIfCancellationRequested();

            var existing = await adventRepository.GetDayAsync(campaign.Id, row.Date, ct);
            if (existing is null)
            {
                existing = new AdventDay
                {
                    CampaignId = campaign.Id,
                    Date = row.Date,
                    Text = row.Text.Trim()
                };
                await adventRepository.UpsertDayAsync(existing, ct);
                created++;
            }
            else
            {
                existing.Text = row.Text.Trim();
                await adventRepository.UpsertDayAsync(existing, ct);
                updated++;
            }
        }

        return new ImportResult
        {
            CreatedDays = created,
            UpdatedDays = updated,
            Errors = Array.Empty<ImportValidationError>()
        };
    }
}
