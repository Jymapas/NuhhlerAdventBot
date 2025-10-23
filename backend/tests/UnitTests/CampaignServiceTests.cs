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

public class CampaignServiceTests
{
    [Fact]
    public async Task CreateCampaignBuildsFullDecember()
    {
        using var context = CreateContext();
        var owner = await SeedUserAsync(context, 1001, "owner");

        var repository = new AdventRepository(context);
        var service = new CampaignService(repository);

        var campaign = await service.CreateDefaultDecemberCampaignAsync(owner.Id, DateTime.UtcNow.Year, CancellationToken.None);

        campaign.Days.Should().HaveCount(31);
        campaign.StartDate.Should().Be(new DateOnly(DateTime.UtcNow.Year, 12, 1));
        campaign.EndDate.Should().Be(new DateOnly(DateTime.UtcNow.Year, 12, 31));
    }

    [Fact]
    public async Task AssignRecipientMarksReady()
    {
        using var context = CreateContext();
        var owner = await SeedUserAsync(context, 2001, "owner");
        var recipient = await SeedUserAsync(context, 2002, "recipient");

        var repository = new AdventRepository(context);
        var service = new CampaignService(repository);

        var campaign = await service.CreateDefaultDecemberCampaignAsync(owner.Id, DateTime.UtcNow.Year, CancellationToken.None);
        var updated = await service.AssignRecipientAsync(campaign.Id, recipient.Id, CancellationToken.None);

        updated.RecipientStatus.Should().Be(RecipientStatus.Ready);
        updated.RecipientUserId.Should().Be(recipient.Id);
        updated.BindToken.Should().BeNull();
    }

    [Fact]
    public async Task DuplicateDraftCreationThrows()
    {
        using var context = CreateContext();
        var owner = await SeedUserAsync(context, 3001, "owner");

        var repository = new AdventRepository(context);
        var service = new CampaignService(repository);

        await service.CreateDefaultDecemberCampaignAsync(owner.Id, DateTime.UtcNow.Year, CancellationToken.None);
        Func<Task> act = () => service.CreateDefaultDecemberCampaignAsync(owner.Id, DateTime.UtcNow.Year, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
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

    private static async Task<User> SeedUserAsync(AppDbContext context, long telegramId, string username)
    {
        var user = new User
        {
            TelegramId = telegramId,
            Username = username,
            FirstName = username,
            CreatedAtUtc = DateTime.UtcNow,
            LastSeenAtUtc = DateTime.UtcNow
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }
}
