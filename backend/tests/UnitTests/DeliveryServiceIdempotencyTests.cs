using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Application.Services;
using Domain.Advent;
using Domain.Delivery;
using Domain.Users;
using FluentAssertions;
using Infrastructure.Persistence;
using Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types.Enums;
using Xunit;

public sealed class DeliveryServiceIdempotencyTests
{
    [Fact]
    public async Task RunScheduledDeliveryTick_FirstTime_ShouldSendAndCreateSentLog()
    {
        await using var fixture = await DeliveryServiceFixture.CreateAsync();

        await fixture.Service.RunScheduledDeliveryTickAsync(CancellationToken.None);

        fixture.Handler.SuccessfulSends.Should().Be(1);
        var log = await fixture.Context.DeliveryLogs.SingleAsync();
        log.Status.Should().Be(DeliveryStatus.Sent);
        log.Attempts.Should().Be(1);
        log.CampaignId.Should().Be(fixture.Campaign.Id);
        log.RecipientUserId.Should().Be(fixture.Recipient.Id);
        log.Date.Should().Be(new DateOnly(2025, 12, 24));
    }

    [Fact]
    public async Task RunScheduledDeliveryTick_SecondCall_ShouldNotResend()
    {
        await using var fixture = await DeliveryServiceFixture.CreateAsync();

        await fixture.Service.RunScheduledDeliveryTickAsync(CancellationToken.None);
        await fixture.Service.RunScheduledDeliveryTickAsync(CancellationToken.None);

        fixture.Handler.SuccessfulSends.Should().Be(1);
        (await fixture.Context.DeliveryLogs.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task RunScheduledDeliveryTick_WithExistingSentLog_ShouldSkip()
    {
        await using var fixture = await DeliveryServiceFixture.CreateAsync();

        fixture.Context.DeliveryLogs.Add(new DeliveryLog
        {
            CampaignId = fixture.Campaign.Id,
            RecipientUserId = fixture.Recipient.Id,
            Date = new DateOnly(2025, 12, 24),
            Status = DeliveryStatus.Sent,
            Attempts = 1,
            CreatedAtUtc = DateTime.UtcNow,
            LastAttemptAtUtc = DateTime.UtcNow
        });
        await fixture.Context.SaveChangesAsync();

        await fixture.Service.RunScheduledDeliveryTickAsync(CancellationToken.None);

        fixture.Handler.RequestCount.Should().Be(0);
        (await fixture.Context.DeliveryLogs.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task RunScheduledDeliveryTick_WithForbiddenError_ShouldMarkFailed()
    {
        await using var fixture = await DeliveryServiceFixture.CreateAsync();
        fixture.Handler.Behavior = TelegramResponseBehavior.Forbidden;

        await fixture.Service.RunScheduledDeliveryTickAsync(CancellationToken.None);

        fixture.Handler.SuccessfulSends.Should().Be(0);
        var log = await fixture.Context.DeliveryLogs.SingleAsync();
        log.Status.Should().Be(DeliveryStatus.Retry);
        log.Attempts.Should().Be(1);
        log.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task RunScheduledDeliveryTick_WithServerError_ShouldCreateRetry()
    {
        await using var fixture = await DeliveryServiceFixture.CreateAsync();
        fixture.Handler.Behavior = TelegramResponseBehavior.ServerError;

        await fixture.Service.RunScheduledDeliveryTickAsync(CancellationToken.None);

        fixture.Handler.SuccessfulSends.Should().Be(0);
        var log = await fixture.Context.DeliveryLogs.SingleAsync();
        log.Status.Should().Be(DeliveryStatus.Retry);
        log.Attempts.Should().Be(1);
        log.Error.Should().NotBeNullOrWhiteSpace();
    }

    private sealed class DeliveryServiceFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        public required AppDbContext Context { get; init; }
        public required DeliveryService Service { get; init; }
        public required FakeTelegramHandler Handler { get; init; }
        public required AdventCampaign Campaign { get; init; }
        public required User Recipient { get; init; }

        private DeliveryServiceFixture(SqliteConnection connection)
        {
            _connection = connection;
        }

        public static async Task<DeliveryServiceFixture> CreateAsync()
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
                TelegramId = 0,
                Username = "owner",
                CreatedAtUtc = DateTime.UtcNow,
                LastSeenAtUtc = DateTime.UtcNow
            };

            var recipient = new User
            {
                TelegramId = 2002,
                Username = "recipient",
                CreatedAtUtc = DateTime.UtcNow,
                LastSeenAtUtc = DateTime.UtcNow
            };

            context.Users.AddRange(owner, recipient);
            await context.SaveChangesAsync();

            var campaign = new AdventCampaign
            {
                OwnerUserId = owner.Id,
                RecipientUserId = recipient.Id,
                RecipientStatus = RecipientStatus.Ready,
                Status = CampaignStatus.Active,
                Name = "Advent-2025",
                StartDate = new DateOnly(2025, 12, 1),
                EndDate = new DateOnly(2025, 12, 31),
                DefaultSendTime = new TimeOnly(8, 30),
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            context.AdventCampaigns.Add(campaign);
            await context.SaveChangesAsync();

            context.AdventDays.Add(new AdventDay
            {
                CampaignId = campaign.Id,
                Date = new DateOnly(2025, 12, 24),
                Text = "hello"
            });
            await context.SaveChangesAsync();

            var handler = new FakeTelegramHandler();
            var httpClient = new HttpClient(handler);
            var botClient = new TelegramBotClient(new TelegramBotClientOptions("123456:TESTTOKEN"), httpClient);

            var timeProvider = CreateUtcProvider();

            var service = new DeliveryService(
                new AdventRepository(context),
                new UserRepository(context),
                new DeliveryLogRepository(context),
                botClient,
                NullLogger<DeliveryService>.Instance,
                timeProvider);

            return new DeliveryServiceFixture(connection)
            {
                Context = context,
                Handler = handler,
                Service = service,
                Campaign = campaign,
                Recipient = recipient
            };
        }

        private static Func<DateTime> CreateUtcProvider()
        {
            var tz = GetAlmatyTimeZone();
            var local = new DateTime(2025, 12, 24, 8, 30, 0, DateTimeKind.Unspecified);
            var utc = TimeZoneInfo.ConvertTimeToUtc(local, tz);
            return () => utc;
        }

        private static TimeZoneInfo GetAlmatyTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Asia/Almaty");
            }
            catch
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Central Asia Standard Time");
            }
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class FakeTelegramHandler : HttpMessageHandler
    {
        public int SuccessfulSends { get; private set; }
        public int RequestCount { get; private set; }
        public List<(long chatId, string text)> Messages { get; } = new();
        public TelegramResponseBehavior Behavior { get; set; } = TelegramResponseBehavior.Success;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;

            var payload = request.Content is not null
                ? await request.Content.ReadAsStringAsync(cancellationToken)
                : string.Empty;

            var (chatId, text) = ParsePayload(payload);

            if (Behavior == TelegramResponseBehavior.Forbidden)
            {
                throw new ApiRequestException("Forbidden: bot was blocked by the user", 403);
            }

            if (Behavior == TelegramResponseBehavior.ServerError)
            {
                throw new ApiRequestException("Internal server error", 500);
            }

            SuccessfulSends++;
            Messages.Add((chatId, text ?? string.Empty));
            return CreateSuccessResponse(text);
        }

        private static (long chatId, string? text) ParsePayload(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                return (0, null);
            }

            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;

            var chatId = root.TryGetProperty("chat_id", out var chatIdProp) && chatIdProp.TryGetInt64(out var parsedChat)
                ? parsedChat
                : 0;

            var text = root.TryGetProperty("text", out var textProp)
                ? textProp.GetString()
                : null;

            return (chatId, text);
        }

        private static HttpResponseMessage CreateSuccessResponse(string? text)
        {
            var response = new
            {
                ok = true,
                result = new
                {
                    message_id = 1,
                    date = 0,
                    chat = new { id = 0, type = ChatType.Private.ToString().ToLowerInvariant() },
                    text = text ?? string.Empty
                }
            };

            return JsonResponse(HttpStatusCode.OK, response);
        }

        private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, object payload)
        {
            var json = JsonSerializer.Serialize(payload);
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }
    }
}

public enum TelegramResponseBehavior
{
    Success,
    Forbidden,
    ServerError
}
