using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Application.Services;
using Domain.Advent;
using Infrastructure.Persistence;
using Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

public class TimeSetupServiceTests
{
    [Fact]
    public async Task SetCampaignTimeUpdatesDefaultSendTime()
    {
        using var context = CreateContext(out var connection);
        var repository = new AdventRepository(context);
        var campaign = await SeedCampaignAsync(context, repository, ownerUserId: 1);
        var service = new TimeSetupService(repository);

        var time = new TimeOnly(9, 45);
        var (ok, error) = await service.SetCampaignTimeAsync(campaign.OwnerUserId, time, CancellationToken.None);

        ok.Should().BeTrue();
        error.Should().BeNull();

        var updated = await repository.GetCampaignAsync(campaign.Id, CancellationToken.None);
        updated!.DefaultSendTime.Should().Be(time);
    }

    [Fact]
    public async Task SetDayTimeUpdatesOverride()
    {
        using var context = CreateContext(out var connection);
        var repository = new AdventRepository(context);
        var campaign = await SeedCampaignAsync(context, repository, ownerUserId: 7);
        var service = new TimeSetupService(repository);

        var date = new DateOnly(2025, 12, 24);
        var time = new TimeOnly(21, 45);

        var (ok, error) = await service.SetDayTimeAsync(campaign.OwnerUserId, date, time, CancellationToken.None);

        ok.Should().BeTrue();
        error.Should().BeNull();

        var day = await repository.GetDayAsync(campaign.Id, date, CancellationToken.None);
        day!.OverrideSendTime.Should().Be(time);
    }

    [Fact]
    public async Task ResetDayTimeClearsOverride()
    {
        using var context = CreateContext(out var connection);
        var repository = new AdventRepository(context);
        var campaign = await SeedCampaignAsync(context, repository, ownerUserId: 9);
        var service = new TimeSetupService(repository);
        var date = new DateOnly(2025, 12, 12);

        await service.SetDayTimeAsync(campaign.OwnerUserId, date, new TimeOnly(10, 15), CancellationToken.None);
        var (ok, error) = await service.ResetDayTimeAsync(campaign.OwnerUserId, date, CancellationToken.None);

        ok.Should().BeTrue();
        error.Should().BeNull();

        var day = await repository.GetDayAsync(campaign.Id, date, CancellationToken.None);
        day!.OverrideSendTime.Should().BeNull();
    }

    [Fact]
    public async Task SetDayTimeOutsideRangeReturnsError()
    {
        using var context = CreateContext(out var connection);
        var repository = new AdventRepository(context);
        var campaign = await SeedCampaignAsync(context, repository, ownerUserId: 15);
        var service = new TimeSetupService(repository);

        var date = new DateOnly(2025, 11, 30);
        var (ok, error) = await service.SetDayTimeAsync(campaign.OwnerUserId, date, new TimeOnly(8, 0), CancellationToken.None);

        ok.Should().BeFalse();
        error.Should().NotBeNull();
        error!.Should().Contain("диапазона");
    }

    private static AppDbContext CreateContext(out SqliteConnection connection)
    {
        connection = new SqliteConnection("Filename=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private static async Task<AdventCampaign> SeedCampaignAsync(AppDbContext context, AdventRepository repository, long ownerUserId)
    {
        var user = new Domain.Users.User
        {
            TelegramId = ownerUserId,
            Username = $"owner{ownerUserId}",
            FirstName = "Owner",
            CreatedAtUtc = DateTime.UtcNow,
            LastSeenAtUtc = DateTime.UtcNow
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        var year = 2025;
        var campaign = new AdventCampaign
        {
            OwnerUserId = user.Id,
            Name = $"Advent-{year}",
            StartDate = new DateOnly(year, 12, 1),
            EndDate = new DateOnly(year, 12, 31),
            DefaultSendTime = new TimeOnly(8, 30),
            Status = CampaignStatus.Draft,
            RecipientStatus = RecipientStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        var days = new List<AdventDay>();
        for (var day = 1; day <= 31; day++)
        {
            days.Add(new AdventDay
            {
                Date = new DateOnly(year, 12, day),
                Text = $"Day {day}"
            });
        }

        await repository.CreateCampaignAsync(campaign, days, CancellationToken.None);
        var stored = await repository.GetCampaignAsync(campaign.Id, CancellationToken.None);
        return stored!;
    }
}
