using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Application.Import;
using Domain.Advent;
using Domain.Users;
using FluentAssertions;
using Infrastructure.Persistence;
using Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

public class ImportServiceTests
{
    [Fact]
    public async Task SuccessfulImportCreatesDays()
    {
        using var context = CreateContext();
        var (campaign, repository) = await SeedCampaignAsync(context);

        var service = new ImportService(repository);
        var rows = CreateRows(1, 3);

        var result = await service.ImportIntoCampaignAsync(campaign.OwnerUserId, campaign.Id, rows, CancellationToken.None);

        result.Errors.Should().BeEmpty();
        result.CreatedDays.Should().Be(3);
        result.UpdatedDays.Should().Be(0);
    }

    [Fact]
    public async Task DuplicateDatesProduceError()
    {
        using var context = CreateContext();
        var (campaign, repository) = await SeedCampaignAsync(context);

        var service = new ImportService(repository);
        var rows = new List<ImportRow>
        {
            new() { Date = new DateOnly(campaign.StartDate.Year, 12, 1), Text = "one" },
            new() { Date = new DateOnly(campaign.StartDate.Year, 12, 1), Text = "duplicate" }
        };

        var result = await service.ImportIntoCampaignAsync(campaign.OwnerUserId, campaign.Id, rows, CancellationToken.None);

        result.Errors.Should().ContainSingle(e => e.Message.Contains("Повторяющаяся дата"));
    }

    [Fact]
    public async Task DateOutsideCampaignRangeProducesError()
    {
        using var context = CreateContext();
        var (campaign, repository) = await SeedCampaignAsync(context);

        var service = new ImportService(repository);
        var rows = new List<ImportRow>
        {
            new() { Date = new DateOnly(campaign.StartDate.Year, 11, 30), Text = "invalid" }
        };

        var result = await service.ImportIntoCampaignAsync(campaign.OwnerUserId, campaign.Id, rows, CancellationToken.None);

        result.Errors.Should().ContainSingle(e => e.Message.Contains("Дата вне диапазона кампании."));
    }

    private static List<ImportRow> CreateRows(int startDay, int count)
    {
        var rows = new List<ImportRow>();
        for (var i = 0; i < count; i++)
        {
            rows.Add(new ImportRow
            {
                Date = new DateOnly(DateTime.UtcNow.Year, 12, startDay + i),
                Text = $"message {i}"
            });
        }

        return rows;
    }

    private static AppDbContext CreateContext()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private static async Task<(AdventCampaign Campaign, AdventRepository Repository)> SeedCampaignAsync(AppDbContext context)
    {
        var user = new User
        {
            TelegramId = 5001,
            Username = "owner",
            CreatedAtUtc = DateTime.UtcNow,
            LastSeenAtUtc = DateTime.UtcNow
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var campaign = new AdventCampaign
        {
            OwnerUserId = user.Id,
            Name = $"Advent-{DateTime.UtcNow.Year}",
            StartDate = new DateOnly(DateTime.UtcNow.Year, 12, 1),
            EndDate = new DateOnly(DateTime.UtcNow.Year, 12, 31),
            DefaultSendTime = new TimeOnly(9, 0),
            Status = CampaignStatus.Draft,
            RecipientStatus = RecipientStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        var repository = new AdventRepository(context);
        await repository.CreateCampaignAsync(campaign, Array.Empty<AdventDay>(), CancellationToken.None);

        var stored = await repository.GetCampaignAsync(campaign.Id, CancellationToken.None)
                     ?? throw new InvalidOperationException("Campaign not found after creation.");

        return (stored, repository);
    }
}
