using System;
using System.Collections.Generic;
using System.Globalization;
using Application.Abstractions;
using Bot.Callbacks;
using Bot.Fsm;
using Bot.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Bot.Handlers.Callbacks;

public sealed class EditTimeCallbackHandler : ICallbackHandler
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly IFsmStorage _fsmStorage;
    private readonly ILogger<EditTimeCallbackHandler> _logger;

    public EditTimeCallbackHandler(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        IFsmStorage fsmStorage,
        ILogger<EditTimeCallbackHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _fsmStorage = fsmStorage;
        _logger = logger;
    }

    public bool CanHandle(string data) => data.StartsWith("edit:time:", StringComparison.OrdinalIgnoreCase);

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

        var dateIso = callbackQuery.Data? ["edit:time:".Length..];
        if (string.IsNullOrWhiteSpace(dateIso))
        {
            await client.AnswerCallbackQuery(callbackQuery.Id, "Не удалось определить дату.", cancellationToken: cancellationToken);
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var adventRepository = scope.ServiceProvider.GetRequiredService<IAdventRepository>();

            var owner = await userRepository.EnsureAsync(callbackQuery.From.Id, callbackQuery.From.Username, callbackQuery.From.FirstName, cancellationToken);
            var campaign = await adventRepository.GetActiveOrDraftByOwnerAsync(owner.Id, cancellationToken);

            if (campaign is null)
            {
                await client.AnswerCallbackQuery(callbackQuery.Id, "Нет активной кампании.", cancellationToken: cancellationToken);
                return;
            }

            var snapshot = new FsmSnapshot
            {
                UserId = callbackQuery.From.Id,
                State = FsmState.SetTimeAwaitValue,
                Payload = new Dictionary<string, string>
                {
                    ["campaignId"] = campaign.Id.ToString(CultureInfo.InvariantCulture),
                    ["date"] = dateIso
                }
            };

            await _fsmStorage.SetAsync(snapshot);

            await client.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);

            await client.SendMessage(
                new ChatId(callbackQuery.Message.Chat.Id),
                $"Пришли время для {dateIso} в формате HH:mm (локально, Asia/Almaty).\nЕсли хочешь вернуть время по умолчанию кампании — напиши default.\n/cancel чтобы отменить.",
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initiate time edit for {Date}", dateIso);
            await client.AnswerCallbackQuery(callbackQuery.Id, "Не удалось перейти в режим изменения времени.", cancellationToken: cancellationToken);
        }
    }
}
