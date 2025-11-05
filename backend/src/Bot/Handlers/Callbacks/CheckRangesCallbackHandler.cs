using System;
using System.Globalization;
using Bot.Callbacks;
using Bot.Security;
using Bot.Keyboards;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Bot.Handlers.Callbacks;

public sealed class CheckRangesCallbackHandler : ICallbackHandler
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<CheckRangesCallbackHandler> _logger;

    public CheckRangesCallbackHandler(
        IConfiguration configuration,
        ILogger<CheckRangesCallbackHandler> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public bool CanHandle(string data) =>
        data.StartsWith("check:range:", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(data, "check:back:ranges", StringComparison.OrdinalIgnoreCase) ||
        data.StartsWith("check:back:days:", StringComparison.OrdinalIgnoreCase);

    public async Task HandleAsync(ITelegramBotClient client, CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        if (callbackQuery.Message is null || callbackQuery.From is null)
            return;

        if (!AuthExtensions.IsAllowedOwner(callbackQuery.From.Id, _configuration))
        {
            await client.AnswerCallbackQuery(
                callbackQuery.Id,
                "Недоступно",
                showAlert: true,
                cancellationToken: cancellationToken);
            return;
        }

        var data = callbackQuery.Data ?? string.Empty;

        try
        {
            if (string.Equals(data, "check:back:ranges", StringComparison.OrdinalIgnoreCase))
            {
                await client.EditMessageText(
                    callbackQuery.Message.Chat.Id,
                    callbackQuery.Message.MessageId,
                    "Выбери диапазон дней",
                    replyMarkup: CheckKeyboards.BuildRangeKeyboard(),
                    cancellationToken: cancellationToken);
                await client.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);
                return;
            }

            if (data.StartsWith("check:back:days:", StringComparison.OrdinalIgnoreCase))
            {
                var rangePartBack = data["check:back:days:".Length..];
                var rangeValues = ParseRange(rangePartBack);
                if (rangeValues is null)
                {
                    await client.AnswerCallbackQuery(callbackQuery.Id, "Не удалось определить диапазон.", cancellationToken: cancellationToken);
                    return;
                }

                await client.EditMessageText(
                    callbackQuery.Message.Chat.Id,
                    callbackQuery.Message.MessageId,
                    $"Выбери день из диапазона {rangeValues.Value.Start}–{rangeValues.Value.End}",
                    replyMarkup: CheckKeyboards.BuildDaysKeyboard(rangeValues.Value.Start, rangeValues.Value.End),
                    cancellationToken: cancellationToken);
                await client.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);
                return;
            }

            var rangePart = data["check:range:".Length..];
            var range = ParseRange(rangePart);
            if (range is null)
            {
                await client.AnswerCallbackQuery(callbackQuery.Id, "Не удалось определить диапазон.", cancellationToken: cancellationToken);
                return;
            }

            await client.EditMessageText(
                callbackQuery.Message.Chat.Id,
                callbackQuery.Message.MessageId,
                $"Выбери день из диапазона {range.Value.Start}–{range.Value.End}",
                replyMarkup: CheckKeyboards.BuildDaysKeyboard(range.Value.Start, range.Value.End),
                cancellationToken: cancellationToken);
            await client.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle range callback {Data}", data);
            await client.AnswerCallbackQuery(callbackQuery.Id, "Произошла ошибка.", cancellationToken: cancellationToken);
        }
    }

    private static (int Start, int End)? ParseRange(string rangePart)
    {
        var parts = rangePart.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 ||
            !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var start) ||
            !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var end))
        {
            return null;
        }

        return (start, end);
    }
}
