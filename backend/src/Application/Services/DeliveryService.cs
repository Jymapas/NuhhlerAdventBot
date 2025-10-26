using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions;
using Domain.Advent;
using Domain.Delivery;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;

namespace Application.Services;

public sealed class DeliveryService
{
    internal const int MaxAttempts = 5;

    private static readonly TimeSpan[] RetryDelays =
    {
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromMinutes(30)
    };

    private readonly IAdventRepository _adventRepository;
    private readonly IUserRepository _userRepository;
    private readonly IDeliveryLogRepository _deliveryLogRepository;
    private readonly ITelegramBotClient _botClient;
    private readonly ILogger<DeliveryService> _logger;
    private readonly TimeZoneInfo _almatyTz;

    public DeliveryService(
        IAdventRepository adventRepository,
        IUserRepository userRepository,
        IDeliveryLogRepository deliveryLogRepository,
        ITelegramBotClient botClient,
        ILogger<DeliveryService> logger)
    {
        _adventRepository = adventRepository;
        _userRepository = userRepository;
        _deliveryLogRepository = deliveryLogRepository;
        _botClient = botClient;
        _logger = logger;
        _almatyTz = GetAlmatyTimeZone();
    }

    public async Task RunScheduledDeliveryTickAsync(CancellationToken ct)
    {
        var nowUtc = DateTime.UtcNow;
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, _almatyTz);
        var todayLocal = DateOnly.FromDateTime(nowLocal);
        var currentMinute = new TimeOnly(nowLocal.Hour, nowLocal.Minute);

        _logger.LogInformation("Delivery tick at {UtcNow} ({LocalTime})", nowUtc, nowLocal);

        await ProcessInitialDeliveriesAsync(todayLocal, currentMinute, ct);
        await ProcessRetryDeliveriesAsync(nowUtc, ct);
    }

    private async Task ProcessInitialDeliveriesAsync(DateOnly todayLocal, TimeOnly currentMinute, CancellationToken ct)
    {
        var campaigns = await _adventRepository.GetActiveCampaignsAsync(ct);

        foreach (var campaign in campaigns)
        {
            if (!campaign.RecipientUserId.HasValue || campaign.RecipientStatus != RecipientStatus.Ready)
                continue;

            var day = campaign.Days.FirstOrDefault(d => d.Date == todayLocal);
            if (day is null)
                continue;

            var sendTime = day.OverrideSendTime ?? campaign.DefaultSendTime;
            if (sendTime.Hour != currentMinute.Hour || sendTime.Minute != currentMinute.Minute)
                continue;

            var recipientId = campaign.RecipientUserId.Value;
            var existingLog = await _deliveryLogRepository.GetLogAsync(campaign.Id, recipientId, todayLocal, ct);
            if (existingLog is not null)
            {
                if (existingLog.Status == DeliveryStatus.Sent)
                    continue;

                if (existingLog.Status == DeliveryStatus.Retry || existingLog.Status == DeliveryStatus.Failed)
                    continue;
            }

            var alreadySent = existingLog is not null && existingLog.Status == DeliveryStatus.Sent
                              || await _deliveryLogRepository.IsSentAsync(campaign.Id, recipientId, todayLocal, ct);
            if (alreadySent)
                continue;

            _logger.LogInformation("Sending message for campaign {CampaignId} date {Date}", campaign.Id, todayLocal);

            var log = new DeliveryLog
            {
                CampaignId = campaign.Id,
                RecipientUserId = recipientId,
                Date = todayLocal,
                Attempts = 0,
                CreatedAtUtc = DateTime.UtcNow,
                LastAttemptAtUtc = DateTime.UtcNow
            };

            await AttemptDeliveryAsync(campaign, day, log, ct);
        }
    }

    private async Task ProcessRetryDeliveriesAsync(DateTime nowUtc, CancellationToken ct)
    {
        var candidates = await _deliveryLogRepository.GetRetryCandidatesAsync(ct);
        foreach (var log in candidates)
        {
            if (log.Attempts >= MaxAttempts)
            {
                await MarkAsFailedAsync(log, "Превышено число попыток", ct);
                continue;
            }

            if (!IsRetryDue(log, nowUtc))
                continue;

            var campaign = await _adventRepository.GetCampaignAsync(log.CampaignId, ct);
            if (campaign is null)
            {
                await MarkAsFailedAsync(log, "Кампания не найдена", ct);
                continue;
            }

            var day = campaign.Days.FirstOrDefault(d => d.Date == log.Date);
            if (day is null)
            {
                await MarkAsFailedAsync(log, "День кампании не найден", ct);
                continue;
            }

            _logger.LogInformation("Retry delivery for campaign {CampaignId} date {Date} attempt #{Attempt}", log.CampaignId, log.Date, log.Attempts + 1);
            await AttemptDeliveryAsync(campaign, day, log, ct);
        }
    }

    private async Task AttemptDeliveryAsync(AdventCampaign campaign, AdventDay day, DeliveryLog log, CancellationToken ct)
    {
        log.Attempts += 1;
        log.LastAttemptAtUtc = DateTime.UtcNow;

        var recipient = await _userRepository.GetByIdAsync(log.RecipientUserId, ct);
        if (recipient is null || recipient.TelegramId == 0)
        {
            await HandleFinalFailureAsync(campaign, log, "Получатель не найден", ct);
            return;
        }

        try
        {
            var message = await _botClient.SendMessage(
                new ChatId(recipient.TelegramId),
                day.Text,
                cancellationToken: ct);

            log.Status = DeliveryStatus.Sent;
            log.TelegramMessageId = message.MessageId;
            log.SentAtUtc = DateTime.UtcNow;
            log.Error = null;

            await _deliveryLogRepository.SaveAsync(log, ct);
        }
        catch (ApiRequestException apiEx)
        {
            var final = IsFinalError(apiEx) || log.Attempts >= MaxAttempts;
            var reason = apiEx.Message;
            if (final)
            {
                await HandleFinalFailureAsync(campaign, log, reason, ct);
            }
            else
            {
                await HandleRetryAsync(log, reason, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected error while sending message for campaign {CampaignId}", campaign.Id);
            var final = log.Attempts >= MaxAttempts;
            if (final)
            {
                await HandleFinalFailureAsync(campaign, log, ex.Message, ct);
            }
            else
            {
                await HandleRetryAsync(log, ex.Message, ct);
            }
        }
    }

    private async Task HandleRetryAsync(DeliveryLog log, string reason, CancellationToken ct)
    {
        log.Status = DeliveryStatus.Retry;
        log.Error = reason;
        await _deliveryLogRepository.SaveAsync(log, ct);
        _logger.LogInformation("Scheduled retry for campaign {CampaignId} date {Date}, attempts {Attempts}", log.CampaignId, log.Date, log.Attempts);
    }

    private async Task HandleFinalFailureAsync(AdventCampaign campaign, DeliveryLog log, string reason, CancellationToken ct)
    {
        log.Status = DeliveryStatus.Failed;
        log.Error = reason;
        await _deliveryLogRepository.SaveAsync(log, ct);
        _logger.LogWarning("Delivery failed for campaign {CampaignId} date {Date}: {Reason}", campaign.Id, log.Date, reason);
        await NotifyOwnerAsync(campaign, log.Date, reason, ct);
    }

    private async Task MarkAsFailedAsync(DeliveryLog log, string reason, CancellationToken ct)
    {
        log.Attempts = Math.Max(log.Attempts, MaxAttempts);
        log.Status = DeliveryStatus.Failed;
        log.Error = reason;
        log.LastAttemptAtUtc = DateTime.UtcNow;
        await _deliveryLogRepository.SaveAsync(log, ct);

        var campaign = await _adventRepository.GetCampaignAsync(log.CampaignId, ct);
        if (campaign is not null)
        {
            await NotifyOwnerAsync(campaign, log.Date, reason, ct);
        }
    }

    private async Task NotifyOwnerAsync(AdventCampaign campaign, DateOnly date, string reason, CancellationToken ct)
    {
        var owner = await _userRepository.GetByIdAsync(campaign.OwnerUserId, ct);
        if (owner?.TelegramId is null or 0)
            return;

        var text = $"Не удалось доставить сообщение за {date:yyyy-MM-dd}. Ошибка: {reason}";

        try
        {
            await _botClient.SendMessage(new ChatId(owner.TelegramId), text, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to notify owner {OwnerId}", campaign.OwnerUserId);
        }
    }

    internal static TimeSpan? GetRetryDelayByAttempts(int attempts) =>
        attempts switch
        {
            0 => TimeSpan.Zero,
            >= MaxAttempts => null,
            _ => RetryDelays[Math.Min(attempts, RetryDelays.Length) - 1]
        };

    internal static bool IsRetryDue(DeliveryLog log, DateTime utcNow)
    {
        var delay = GetRetryDelayByAttempts(log.Attempts);
        if (delay is null)
        {
            return false;
        }

        var next = log.LastAttemptAtUtc + delay.Value;
        return utcNow >= next;
    }

    private static bool IsFinalError(ApiRequestException ex)
    {
        if (ex.ErrorCode == 403)
        {
            return true;
        }

        if (ex.ErrorCode == 400 && ex.Message?.Contains("blocked", StringComparison.OrdinalIgnoreCase) == true)
        {
            return true;
        }

        return false;
    }

    private static TimeZoneInfo GetAlmatyTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Almaty");
        }
        catch (TimeZoneNotFoundException)
        {
        }
        catch (InvalidTimeZoneException)
        {
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Central Asia Standard Time");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Cannot locate Asia/Almaty timezone.", ex);
        }
    }
}
