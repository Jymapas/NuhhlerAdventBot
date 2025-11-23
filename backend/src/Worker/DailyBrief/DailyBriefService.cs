using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions;
using Bot.Keyboards;
using Domain.Advent;
using Domain.Users;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;

using Shared.Logging;
using UserEntity = Domain.Users.User;

namespace Worker.DailyBrief;

public sealed class DailyBriefService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITelegramBotClient _botClient;
    private readonly ILogger<DailyBriefService> _logger;
    private readonly TimeZoneInfo _almatyTz;

    private DateOnly? _lastBriefDate;

    public DailyBriefService(IServiceScopeFactory scopeFactory, ITelegramBotClient botClient, ILogger<DailyBriefService> logger)
    {
        _scopeFactory = scopeFactory;
        _botClient = botClient;
        _logger = logger;
        _almatyTz = GetAlmatyTimeZone();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var nowUtc = DateTime.UtcNow;
                var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, _almatyTz);
                if (ShouldSendBrief(nowLocal, _lastBriefDate))
                {
                    var today = DateOnly.FromDateTime(nowLocal);
                    _logger.LogInformation("Daily brief tick for {Date}", today);
                    await RunDailyBriefTickAsync(today, stoppingToken);
                    _lastBriefDate = today;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Daily brief tick failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    internal static bool ShouldSendBrief(DateTime localNow, DateOnly? lastSentDate)
    {
        if (localNow.Hour == 8 && localNow.Minute == 0)
        {
            var today = DateOnly.FromDateTime(localNow);
            return lastSentDate is null || lastSentDate.Value != today;
        }

        return false;
    }

    private async Task RunDailyBriefTickAsync(DateOnly todayLocal, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var adventRepository = scope.ServiceProvider.GetRequiredService<IAdventRepository>();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();

        var campaigns = await adventRepository.GetCampaignsForDailyBriefAsync(ct);
        foreach (var campaign in campaigns)
        {
            try
            {
                if (todayLocal < campaign.StartDate || todayLocal > campaign.EndDate)
                    continue;

                if (!campaign.RecipientUserId.HasValue)
                    continue;

                var day = campaign.Days.FirstOrDefault(d => d.Date == todayLocal);
                if (day is null)
                    continue;

                var recipient = await userRepository.GetByIdAsync(campaign.RecipientUserId.Value, ct);
                if (recipient is null)
                    continue;

                var owner = await userRepository.GetByIdAsync(campaign.OwnerUserId, ct);
                if (owner?.TelegramId is null or 0)
                    continue;

                var dateIso = todayLocal.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                using var campaignScope = LogScopes.WithCampaign(campaign.Id, dateIso);

                var sendTime = day.OverrideSendTime ?? campaign.DefaultSendTime;
                var recipientDisplay = BuildRecipientDisplay(recipient);
                var message = BuildPreviewMessage(campaign, day, todayLocal, sendTime, recipientDisplay);
                var keyboard = DailyBriefKeyboards.BuildDailyBriefKeyboard(campaign.Id, todayLocal.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

                await _botClient.SendMessage(new ChatId(owner.TelegramId), message, replyMarkup: keyboard, cancellationToken: ct);
                _logger.LogInformation("Daily brief sent for campaign {CampaignId} date {Date}", campaign.Id, dateIso);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send daily brief for campaign {CampaignId}", campaign.Id);
            }
        }
    }

    private static string BuildRecipientDisplay(UserEntity recipient)
    {
        if (!string.IsNullOrWhiteSpace(recipient.Username))
            return $"@{recipient.Username}";

        return recipient.TelegramId != 0 ? $"id:{recipient.TelegramId}" : "неизвестный";
    }

    private static string BuildPreviewMessage(AdventCampaign campaign, AdventDay day, DateOnly date, TimeOnly sendTime, string recipientDisplay)
    {
        var builder = new StringBuilder();
        var timeFormatted = sendTime.ToString("HH:mm", CultureInfo.InvariantCulture);

        if (campaign.Status == CampaignStatus.Paused)
        {
            builder.AppendLine($"Сегодня ({date:yyyy-MM-dd}) кампания на паузе.");
            builder.AppendLine($"Сообщение для {recipientDisplay} ({timeFormatted}):");
        }
        else
        {
            builder.AppendLine($"Сегодня ({date:yyyy-MM-dd}) планируется отправить получателю {recipientDisplay} в {timeFormatted}:");
        }

        builder.AppendLine();
        builder.AppendLine(day.Text ?? string.Empty);
        builder.AppendLine();
        builder.AppendLine($"Статус кампании: {campaign.Status}");

        if (campaign.Status == CampaignStatus.Paused)
        {
            builder.AppendLine("⚠ Кампания на паузе — сегодня не будет отправки автоматически.");
        }

        return builder.ToString();
    }

    private static TimeZoneInfo GetAlmatyTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Almaty");
        }
        catch
        {
        }

        return TimeZoneInfo.FindSystemTimeZoneById("Central Asia Standard Time");
    }
}
