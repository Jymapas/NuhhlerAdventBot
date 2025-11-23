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
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public sealed class ImportServiceTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private AppDbContext _context = null!;
    private AdventRepository _adventRepository = null!;
    private ImportService _service = null!;
    private AdventCampaign _campaign = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        await _connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new AppDbContext(options);
        await _context.Database.EnsureCreatedAsync();

        var owner = new User
        {
            TelegramId = 123,
            Username = "owner",
            CreatedAtUtc = DateTime.UtcNow,
            LastSeenAtUtc = DateTime.UtcNow
        };

        _context.Users.Add(owner);
        await _context.SaveChangesAsync();

        _campaign = new AdventCampaign
        {
            OwnerUserId = owner.Id,
            Name = $"Advent-{DateTime.UtcNow.Year}",
            StartDate = new DateOnly(2025, 12, 1),
            EndDate = new DateOnly(2025, 12, 31),
            DefaultSendTime = new TimeOnly(8, 30),
            Status = CampaignStatus.Draft,
            RecipientStatus = RecipientStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _context.AdventCampaigns.Add(_campaign);
        await _context.SaveChangesAsync();

        _adventRepository = new AdventRepository(_context);
        _service = new ImportService(_adventRepository, NullLogger<ImportService>.Instance);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task Import_AllValidRows_ShouldCreateDays()
    {
        var rows = new List<ImportRow>
        {
            new() { Date = new DateOnly(2025, 12, 1), Text = "day1" },
            new() { Date = new DateOnly(2025, 12, 2), Text = "day2" },
            new() { Date = new DateOnly(2025, 12, 3), Text = "day3" }
        };

        var result = await _service.ImportIntoCampaignAsync(_campaign.OwnerUserId, _campaign.Id, rows, CancellationToken.None);

        result.Errors.Should().BeEmpty();
        (result.CreatedDays + result.UpdatedDays).Should().Be(3);

        var days = await _context.AdventDays.ToListAsync();
        days.Should().HaveCount(3);
        days.Should().Contain(d => d.Date == new DateOnly(2025, 12, 1) && d.Text == "day1");
        days.Should().Contain(d => d.Date == new DateOnly(2025, 12, 2) && d.Text == "day2");
        days.Should().Contain(d => d.Date == new DateOnly(2025, 12, 3) && d.Text == "day3");
    }

    [Fact]
    public async Task Import_DuplicateDateInFile_ShouldFailAndNotTouchDb()
    {
        var rows = new List<ImportRow>
        {
            new() { Date = new DateOnly(2025, 12, 5), Text = "first" },
            new() { Date = new DateOnly(2025, 12, 5), Text = "second" }
        };

        var result = await _service.ImportIntoCampaignAsync(_campaign.OwnerUserId, _campaign.Id, rows, CancellationToken.None);

        result.Errors.Should().NotBeEmpty();
        result.Errors.Should().Contain(e => e.Message.Contains("Повторяющаяся дата", StringComparison.OrdinalIgnoreCase));
        (result.CreatedDays + result.UpdatedDays).Should().Be(0);

        (await _context.AdventDays.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Import_DateOutOfCampaignRange_ShouldReturnError()
    {
        var rows = new List<ImportRow>
        {
            new() { Date = new DateOnly(2025, 11, 30), Text = "invalid" }
        };

        var result = await _service.ImportIntoCampaignAsync(_campaign.OwnerUserId, _campaign.Id, rows, CancellationToken.None);

        result.Errors.Should().HaveCount(1);
        result.Errors[0].Message.Should().NotBeNull();
        result.Errors[0].Message.Contains("диапазона", StringComparison.OrdinalIgnoreCase).Should().BeTrue();
        (await _context.AdventDays.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Import_EmptyText_ShouldReturnError()
    {
        var rows = new List<ImportRow>
        {
            new() { Date = new DateOnly(2025, 12, 10), Text = "   " }
        };

        var result = await _service.ImportIntoCampaignAsync(_campaign.OwnerUserId, _campaign.Id, rows, CancellationToken.None);

        result.Errors.Should().ContainSingle();
        result.Errors[0].Message.Should().Contain("Текст");
        (await _context.AdventDays.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Import_TooLongText_ShouldReturnError()
    {
        var rows = new List<ImportRow>
        {
            new() { Date = new DateOnly(2025, 12, 15), Text = new string('x', 5000) }
        };

        var result = await _service.ImportIntoCampaignAsync(_campaign.OwnerUserId, _campaign.Id, rows, CancellationToken.None);

        result.Errors.Should().ContainSingle();
        result.Errors[0].Message.Should().Contain("4096");
        (await _context.AdventDays.CountAsync()).Should().Be(0);
    }
}
