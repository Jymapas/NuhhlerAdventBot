using System;
using System.Collections.Generic;
using System.Globalization;
using Application.Abstractions;
using Domain.Advent;
using Microsoft.Extensions.Logging;
using Shared.Logging;

namespace Application.Import;

public sealed class ImportService : IImportService
{
    private readonly IAdventRepository _adventRepository;
    private readonly ILogger<ImportService> _logger;

    public ImportService(IAdventRepository adventRepository, ILogger<ImportService> logger)
    {
        _adventRepository = adventRepository;
        _logger = logger;
    }

    public async Task<ImportResult> ImportIntoCampaignAsync(
        long ownerUserId,
        long campaignId,
        IReadOnlyList<ImportRow> rows,
        CancellationToken ct)
    {
        using var campaignScope = LogScopes.WithCampaign(campaignId, null);
        _logger.LogInformation("Import started with {RowCount} rows", rows.Count);

        var campaign = await _adventRepository.GetCampaignAsync(campaignId, ct);
        if (campaign is null || campaign.OwnerUserId != ownerUserId)
        {
            _logger.LogWarning("Campaign not found or ownership mismatch");
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
            var dateIso = row.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            if (string.IsNullOrWhiteSpace(row.Text) || row.Text.Trim().Length == 0)
            {
                errors.Add(LogValidationError(campaignId, dateIso, rowNumber, "Текст пустой."));
                continue;
            }

            if (row.Text.Length > 4096)
            {
                errors.Add(LogValidationError(campaignId, dateIso, rowNumber, "Текст длиннее 4096 символов."));
                continue;
            }

            if (row.Date.Year != year || row.Date < start || row.Date > end)
            {
                errors.Add(LogValidationError(campaignId, dateIso, rowNumber, "Дата вне диапазона кампании."));
                continue;
            }

            if (!seenDates.Add(row.Date))
            {
                errors.Add(LogValidationError(campaignId, dateIso, rowNumber, "Повторяющаяся дата в файле."));
                continue;
            }

            validRows.Add((row, rowNumber));
        }

        if (errors.Count > 0)
        {
            _logger.LogWarning("Import finished with validation errors: {ErrorCount}", errors.Count);
            return new ImportResult
            {
                CreatedDays = 0,
                UpdatedDays = 0,
                Errors = errors
            };
        }

        var created = 0;
        var updated = 0;

        foreach (var (row, rowNumber) in validRows)
        {
            ct.ThrowIfCancellationRequested();

            var dateIso = row.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            using var rowScope = LogScopes.WithCampaign(campaign.Id, dateIso);

            var existing = await _adventRepository.GetDayAsync(campaign.Id, row.Date, ct);
            if (existing is null)
            {
                existing = new AdventDay
                {
                    CampaignId = campaign.Id,
                    Date = row.Date,
                    Text = row.Text.Trim()
                };
                await _adventRepository.UpsertDayAsync(existing, ct);
                created++;
                _logger.LogInformation("Created day from import (row {RowNumber})", rowNumber);
            }
            else
            {
                existing.Text = row.Text.Trim();
                await _adventRepository.UpsertDayAsync(existing, ct);
                updated++;
                _logger.LogInformation("Updated day from import (row {RowNumber})", rowNumber);
            }
        }

        _logger.LogInformation("Import completed: created={Created}, updated={Updated}", created, updated);

        return new ImportResult
        {
            CreatedDays = created,
            UpdatedDays = updated,
            Errors = Array.Empty<ImportValidationError>()
        };
    }

    private ImportValidationError LogValidationError(long campaignId, string dateIso, int rowNumber, string message)
    {
        using var scope = LogScopes.WithCampaign(campaignId, dateIso);
        _logger.LogWarning("Import validation error at row {RowNumber}: {Message}", rowNumber, message);
        return new ImportValidationError
        {
            RowNumber = rowNumber,
            Message = message
        };
    }
}
