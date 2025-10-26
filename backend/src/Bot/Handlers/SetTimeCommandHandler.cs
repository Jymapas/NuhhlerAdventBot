using System;
using System.Globalization;
using Application.Abstractions;
using Application.Services;
using Bot.Commands;
using Bot.Fsm;
using Bot.Handlers.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Bot.Handlers;

public sealed class SetTimeCommandHandler : HandlerBase, ICommandHandler
{
    private readonly IServiceScopeFactory _scopeFactory;

    private static readonly string UsageMessage =
        "/set_time HH:mm — задать время по умолчанию для кампании\n" +
        "/set_time YYYY-MM-DD HH:mm — задать время для конкретного дня\n" +
        "/set_time YYYY-MM-DD default — вернуть день к времени кампании";

    public SetTimeCommandHandler(
        IFsmStorage fsmStorage,
        IConfiguration configuration,
        ILogger<SetTimeCommandHandler> logger,
        IServiceScopeFactory scopeFactory)
        : base(fsmStorage, configuration, logger)
    {
        _scopeFactory = scopeFactory;
    }

    public bool CanHandle(string command) => string.Equals(command, "/set_time", StringComparison.OrdinalIgnoreCase);

    public async Task HandleAsync(ITelegramBotClient client, Update update, string command, string? args, CancellationToken cancellationToken)
    {
        if (update.Message?.From is null)
            return;

        EnsureOwner(update.Message.From.Id, update.Message.From.Username);

        using var scope = _scopeFactory.CreateScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var timeSetupService = scope.ServiceProvider.GetRequiredService<TimeSetupService>();

        var user = await userRepository.EnsureAsync(update.Message.From.Id, update.Message.From.Username, update.Message.From.FirstName, cancellationToken);
        var chatId = new ChatId(GetChatId(update));

        var trimmedArgs = args?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedArgs))
        {
            await client.SendMessage(chatId, UsageMessage, cancellationToken: cancellationToken);
            return;
        }

        var parts = trimmedArgs.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 1)
        {
            if (!TryParseTime(parts[0], out var time))
            {
                await client.SendMessage(chatId, "Неверный формат. Используйте HH:mm.", cancellationToken: cancellationToken);
                return;
            }

            var result = await timeSetupService.SetCampaignTimeAsync(user.Id, time, cancellationToken);
            if (!result.ok)
            {
                await client.SendMessage(chatId, result.error ?? "Не удалось обновить время кампании.", cancellationToken: cancellationToken);
                return;
            }

            await client.SendMessage(chatId, $"Время кампании обновлено на {time:HH\\:mm}", cancellationToken: cancellationToken);
            return;
        }

        if (parts.Length == 2)
        {
            if (!DateOnly.TryParseExact(parts[0], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                await client.SendMessage(chatId, "Неверный формат даты. Используйте YYYY-MM-DD.", cancellationToken: cancellationToken);
                return;
            }

            if (string.Equals(parts[1], "default", StringComparison.OrdinalIgnoreCase))
            {
                var resetResult = await timeSetupService.ResetDayTimeAsync(user.Id, date, cancellationToken);
                if (!resetResult.ok)
                {
                    await client.SendMessage(chatId, resetResult.error ?? "Не удалось обновить время дня.", cancellationToken: cancellationToken);
                    return;
                }

                await client.SendMessage(chatId, $"Для {date:yyyy-MM-dd} теперь используется время кампании.", cancellationToken: cancellationToken);
                return;
            }

            if (!TryParseTime(parts[1], out var time))
            {
                await client.SendMessage(chatId, "Неверный формат. Используйте HH:mm.", cancellationToken: cancellationToken);
                return;
            }

            var dayResult = await timeSetupService.SetDayTimeAsync(user.Id, date, time, cancellationToken);
            if (!dayResult.ok)
            {
                await client.SendMessage(chatId, dayResult.error ?? "Не удалось обновить время дня.", cancellationToken: cancellationToken);
                return;
            }

            await client.SendMessage(chatId, $"Для {date:yyyy-MM-dd} установлено время {time:HH\\:mm}", cancellationToken: cancellationToken);
            return;
        }

        await client.SendMessage(chatId, UsageMessage, cancellationToken: cancellationToken);
    }

    private static bool TryParseTime(string value, out TimeOnly time) =>
        TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
}
