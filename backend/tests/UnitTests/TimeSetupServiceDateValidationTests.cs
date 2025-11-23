using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Services;
using Domain.Advent;
using Domain.Users;
using FluentAssertions;
using Infrastructure.Persistence;
using Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

public class TimeSetupServiceDateValidationTests
{
    [Fact]
    public async Task SetDayTime_DateWithinCampaign_ShouldSucceed()
    {
        await using var fixture = await TimeSetupFixture.CreateAsync();

        var result = await fixture.Service.SetDayTimeAsync(fixture.Campaign.OwnerUserId, new DateOnly(2025, 12, 24), new TimeOnly(21, 45), CancellationToken.None);

        result.ok.Should().BeTrue();
        result.error.Should().BeNull();

        var day = await fixture.Context.AdventDays.SingleAsync(d => d.Date == new DateOnly(2025, 12, 24));
        day.OverrideSendTime.Should().Be(new TimeOnly(21, 45));
    }

    [Fact]
    public async Task SetDayTime_DateBeforeCampaign_ShouldFail()
    {
        await using var fixture = await TimeSetupFixture.CreateAsync();

        var result = await fixture.Service.SetDayTimeAsync(fixture.Campaign.OwnerUserId, new DateOnly(2025, 11, 30), new TimeOnly(10, 0), CancellationToken.None);

        result.ok.Should().BeFalse();
        result.error.Should().NotBeNull();
        result.error!.Contains("диапаз", StringComparison.OrdinalIgnoreCase).Should().BeTrue();

        var day = await fixture.Context.AdventDays.SingleAsync(d => d.Date == new DateOnly(2025, 12, 24));
        day.OverrideSendTime.Should().BeNull();
    }

    [Fact]
    public async Task SetDayTime_DateAfterCampaign_ShouldFail()
    {
        await using var fixture = await TimeSetupFixture.CreateAsync();

        var result = await fixture.Service.SetDayTimeAsync(fixture.Campaign.OwnerUserId, new DateOnly(2026, 1, 1), new TimeOnly(10, 0), CancellationToken.None);

        result.ok.Should().BeFalse();
        result.error.Should().NotBeNull();
        result.error!.Contains("диапаз", StringComparison.OrdinalIgnoreCase).Should().BeTrue();
    }

    [Fact]
    public async Task ResetDayTime_DateOutOfRange_ShouldFail()
    {
        await using var fixture = await TimeSetupFixture.CreateAsync();

        var result = await fixture.Service.ResetDayTimeAsync(fixture.Campaign.OwnerUserId, new DateOnly(2025, 11, 30), CancellationToken.None);

        result.ok.Should().BeFalse();
        result.error.Should().NotBeNull();
        result.error!.Contains("диапаз", StringComparison.OrdinalIgnoreCase).Should().BeTrue();
    }

    private sealed class TimeSetupFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        public required AppDbContext Context { get; init; }
        public required TimeSetupService Service { get; init; }
        public required AdventCampaign Campaign { get; init; }

        private TimeSetupFixture(SqliteConnection connection)
        {
            _connection = connection;
        }

        public static async Task<TimeSetupFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Filename=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            var context = new AppDbContext(options);
            await context.Database.EnsureCreatedAsync();

            var owner = new User
            {
                TelegramId = 100,
                Username = "owner",
                CreatedAtUtc = DateTime.UtcNow,
                LastSeenAtUtc = DateTime.UtcNow
            };

            context.Users.Add(owner);
            await context.SaveChangesAsync();

            var campaign = new AdventCampaign
            {
                OwnerUserId = owner.Id,
                Name = "Advent-2025",
                StartDate = new DateOnly(2025, 12, 1),
                EndDate = new DateOnly(2025, 12, 31),
                DefaultSendTime = new TimeOnly(8, 30),
                Status = CampaignStatus.Active,
                RecipientStatus = RecipientStatus.Pending,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            context.AdventCampaigns.Add(campaign);
            await context.SaveChangesAsync();

            context.AdventDays.Add(new AdventDay
            {
                CampaignId = campaign.Id,
                Date = new DateOnly(2025, 12, 24),
                Text = "text"
            });

            await context.SaveChangesAsync();

            var repository = new AdventRepository(context);

            return new TimeSetupFixture(connection)
            {
                Context = context,
                Service = new TimeSetupService(repository),
                Campaign = campaign
            };
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
