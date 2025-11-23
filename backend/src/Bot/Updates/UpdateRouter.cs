using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Application.Abstractions;
using Application.Import;
using Application.Services;
using Bot.Callbacks;
using Bot.Commands;
using Bot.Fsm;
using Bot.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Bot.Updates;

public sealed class UpdateRouter : IUpdateRouter
{
    private readonly ILogger<UpdateRouter> _logger;
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly ICallbackDispatcher _callbackDispatcher;
    private readonly IFsmStorage _fsmStorage;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;

    public UpdateRouter(
        ILogger<UpdateRouter> logger,
        ICommandDispatcher commandDispatcher,
        ICallbackDispatcher callbackDispatcher,
        IFsmStorage fsmStorage,
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _commandDispatcher = commandDispatcher;
        _callbackDispatcher = callbackDispatcher;
        _fsmStorage = fsmStorage;
        _scopeFactory = scopeFactory;
        _configuration = configuration;
    }

    public async Task HandleAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        var actor = update.Message?.From ?? update.CallbackQuery?.From ?? update.MyChatMember?.From;
        using var scope = LogScopes.WithUpdate(update.Id, actor?.Id, actor?.Username);

        try
        {
            _logger.LogInformation("Processing update {Type}", update.Type);

            switch (update.Type)
            {
                case UpdateType.Message when update.Message is { Text: { } text }:
                {
                    if (update.Message.From is not null)
                    {
                        var snapshot = await _fsmStorage.GetAsync(update.Message.From.Id);
                        if (snapshot is not null && !text.TrimStart().StartsWith("/", StringComparison.Ordinal))
                        {
                            var handled = await HandleTextInputAsync(botClient, update.Message, text, snapshot, cancellationToken);
                            if (handled)
                            {
                                return;
                            }
                        }
                    }

                    if (!text.TrimStart().StartsWith("/", StringComparison.Ordinal))
                    {
                        return;
                    }

                    var command = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries)[0];
                    var user = update.Message!.From;
                    _logger.LogInformation("Dispatching command {Command} from user {UserId} ({Username})", command, user?.Id, user?.Username);

                    await _commandDispatcher.DispatchAsync(botClient, update, cancellationToken);
                    break;
                }

                case UpdateType.Message when update.Message is { Document: not null }:
                {
                    _logger.LogInformation("Received document for import");
                    await HandleImportFileAsync(botClient, update.Message, cancellationToken);
                    break;
                }

                case UpdateType.CallbackQuery when update.CallbackQuery is { } callback:
                {
                    _logger.LogInformation("Dispatching callback {Data}", callback.Data);
                    await _callbackDispatcher.DispatchAsync(botClient, callback, cancellationToken);
                    break;
                }

                case UpdateType.MyChatMember when update.MyChatMember is { } chatMember:
                {
                    _logger.LogInformation("Chat member update: status {Status}", chatMember.NewChatMember.Status);
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // graceful shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Router failure");
        }
    }

    private async Task HandleImportFileAsync(ITelegramBotClient client, Message message, CancellationToken ct)
    {
        if (message.Document is null || message.From is null)
        {
            return;
        }

        if (!AuthExtensions.IsAllowedOwner(message.From.Id, _configuration))
        {
            await client.SendMessage(new ChatId(message.Chat.Id), "У вас нет прав использовать эту команду.", cancellationToken: ct);
            await _fsmStorage.ClearAsync(message.From.Id);
            return;
        }

        var snapshot = await _fsmStorage.GetAsync(message.From.Id);
        if (snapshot is null || snapshot.State != FsmState.ImportAwaitFile)
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var importParser = scope.ServiceProvider.GetRequiredService<IImportParser>();
        var importService = scope.ServiceProvider.GetRequiredService<IImportService>();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();

        try
        {
            if (!snapshot.Payload.TryGetValue("campaignId", out var campaignIdRaw) ||
                !long.TryParse(campaignIdRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var campaignId))
            {
                await client.SendMessage(new ChatId(message.Chat.Id), "Не удалось определить кампанию для импорта. Начните заново командой /import.", cancellationToken: ct);
                await _fsmStorage.ClearAsync(message.From.Id);
                return;
            }

            using var campaignScope = LogScopes.WithCampaign(campaignId, null);
            _logger.LogInformation("Starting import for campaign {CampaignId}", campaignId);

            long ownerId;
            if (snapshot.Payload.TryGetValue("ownerId", out var ownerIdRaw) &&
                long.TryParse(ownerIdRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedOwnerId))
            {
                ownerId = parsedOwnerId;
            }
            else
            {
                var owner = await userRepository.EnsureAsync(message.From.Id, message.From.Username, message.From.FirstName, ct);
                ownerId = owner.Id;
            }

            var document = message.Document;
            if (document.FileSize.HasValue && document.FileSize.Value > 5_000_000)
            {
                await client.SendMessage(new ChatId(message.Chat.Id), "Файл слишком большой. Пришлите документ до 5 МБ.", cancellationToken: ct);
                await _fsmStorage.ClearAsync(message.From.Id);
                return;
            }

            var file = await client.GetFile(document.FileId, ct);
            if (string.IsNullOrEmpty(file.FilePath))
            {
                await client.SendMessage(new ChatId(message.Chat.Id), "Не удалось скачать файл. Попробуйте ещё раз.", cancellationToken: ct);
                await _fsmStorage.ClearAsync(message.From.Id);
                return;
            }

            await using var stream = new MemoryStream();
            await client.DownloadFile(file.FilePath, stream, cancellationToken: ct);
            stream.Seek(0, SeekOrigin.Begin);

            var rows = await importParser.ParseAsync(stream, document.FileName ?? "import", ct);
            var result = await importService.ImportIntoCampaignAsync(ownerId, campaignId, rows, ct);

            _logger.LogInformation("Import processed: created={Created} updated={Updated} errors={ErrorsCount}", result.CreatedDays, result.UpdatedDays, result.Errors.Count);

            if (result.Errors.Count == 0)
            {
                var total = result.CreatedDays + result.UpdatedDays;
                await client.SendMessage(
                    new ChatId(message.Chat.Id),
                    $"Импортировано {total} дней; обновлено {result.UpdatedDays} дней. Ошибок нет.",
                    cancellationToken: ct);
            }
            else
            {
                var details = result.Errors
                    .Take(5)
                    .Select(e => $"- строка {e.RowNumber}: {e.Message}");

                var messageText = "Обнаружены ошибки:\n" + string.Join("\n", details);
                if (result.Errors.Count > 5)
                {
                    messageText += "\n…";
                }

                await client.SendMessage(new ChatId(message.Chat.Id), messageText, cancellationToken: ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import file for user {UserId}", message.From.Id);
            await client.SendMessage(new ChatId(message.Chat.Id), "Не удалось обработать файл. Убедитесь в корректности формата.", cancellationToken: ct);
        }
        finally
        {
            await _fsmStorage.ClearAsync(message.From.Id);
        }
    }

    private async Task<bool> HandleTextInputAsync(ITelegramBotClient client, Message message, string text, FsmSnapshot snapshot, CancellationToken ct)
    {
        switch (snapshot.State)
        {
            case FsmState.EditAwaitText:
                await HandleEditTextAsync(client, message, text, snapshot, ct);
                return true;
            case FsmState.SetTimeAwaitValue:
                await HandleEditTimeAsync(client, message, text, snapshot, ct);
                return true;
            default:
                return false;
        }
    }

    private async Task HandleEditTextAsync(ITelegramBotClient client, Message message, string text, FsmSnapshot snapshot, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            await client.SendMessage(new ChatId(message.Chat.Id), "Текст не может быть пустым. Попробуйте снова или используйте /cancel.", cancellationToken: ct);
            return;
        }

        if (message.From is not null && !AuthExtensions.IsAllowedOwner(message.From.Id, _configuration))
        {
            await client.SendMessage(new ChatId(message.Chat.Id), "У вас нет прав использовать эту команду.", cancellationToken: ct);
            await _fsmStorage.ClearAsync(message.From!.Id);
            return;
        }

        if (text.Length > 4096)
        {
            await client.SendMessage(new ChatId(message.Chat.Id), "Текст слишком длинный (максимум 4096 символов).", cancellationToken: ct);
            return;
        }

        if (!snapshot.Payload.TryGetValue("campaignId", out var campaignIdRaw) ||
            !long.TryParse(campaignIdRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var campaignId) ||
            !snapshot.Payload.TryGetValue("date", out var dateIso) ||
            !DateOnly.TryParse(dateIso, CultureInfo.InvariantCulture, out var date))
        {
            await client.SendMessage(new ChatId(message.Chat.Id), "Не удалось определить контекст редактирования. Начните заново через /check.", cancellationToken: ct);
            await _fsmStorage.ClearAsync(message.From!.Id);
            return;
        }

        using var campaignScope = LogScopes.WithCampaign(campaignId, dateIso);
        _logger.LogInformation("Updating text via FSM for {Date}", dateIso);

        using var scope = _scopeFactory.CreateScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var adventRepository = scope.ServiceProvider.GetRequiredService<IAdventRepository>();

        var owner = await userRepository.EnsureAsync(message.From!.Id, message.From.Username, message.From.FirstName, ct);
        var campaign = await adventRepository.GetActiveOrDraftByOwnerAsync(owner.Id, ct);

        if (campaign is null || campaign.Id != campaignId)
        {
            await client.SendMessage(new ChatId(message.Chat.Id), "Нет активной кампании. Создайте её через /new_advent.", cancellationToken: ct);
            await _fsmStorage.ClearAsync(message.From.Id);
            return;
        }

        var day = await adventRepository.GetDayAsync(campaign.Id, date, ct)
                  ?? new Domain.Advent.AdventDay { CampaignId = campaign.Id, Date = date };

        day.Text = text.Trim();
        await adventRepository.UpsertDayAsync(day, ct);

        await _fsmStorage.ClearAsync(message.From.Id);
        _logger.LogInformation("Text updated via FSM for {Date}", dateIso);
        await client.SendMessage(new ChatId(message.Chat.Id), "Сохранено ✅", cancellationToken: ct);
    }

    private async Task HandleEditTimeAsync(ITelegramBotClient client, Message message, string text, FsmSnapshot snapshot, CancellationToken ct)
    {
        if (!snapshot.Payload.TryGetValue("date", out var dateIso) ||
            !DateOnly.TryParse(dateIso, CultureInfo.InvariantCulture, out var date))
        {
            await client.SendMessage(new ChatId(message.Chat.Id), "Не удалось определить контекст редактирования. Начните заново через /check.", cancellationToken: ct);
            await _fsmStorage.ClearAsync(message.From!.Id);
            return;
        }

        if (message.From is not null && !AuthExtensions.IsAllowedOwner(message.From.Id, _configuration))
        {
            await client.SendMessage(new ChatId(message.Chat.Id), "У вас нет прав использовать эту команду.", cancellationToken: ct);
            await _fsmStorage.ClearAsync(message.From!.Id);
            return;
        }

        using var campaignScope = LogScopes.WithCampaign(null, dateIso);

        using var scope = _scopeFactory.CreateScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var timeSetupService = scope.ServiceProvider.GetRequiredService<TimeSetupService>();

        var owner = await userRepository.EnsureAsync(message.From!.Id, message.From.Username, message.From.FirstName, ct);

        var trimmed = text.Trim();
        TimeOnly? overrideTime = null;
        if (!trimmed.Equals("default", StringComparison.OrdinalIgnoreCase) && trimmed != "-")
        {
            if (!TimeOnly.TryParseExact(trimmed, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                await client.SendMessage(new ChatId(message.Chat.Id), "Некорректный формат времени. Используйте HH:mm или default.", cancellationToken: ct);
                return;
            }

            overrideTime = parsed;
        }

        if (overrideTime is null)
        {
            var (ok, error) = await timeSetupService.ResetDayTimeAsync(owner.Id, date, ct);
            if (!ok)
            {
                await client.SendMessage(new ChatId(message.Chat.Id), error ?? "Не удалось обновить время дня.", cancellationToken: ct);
                return;
            }

            await _fsmStorage.ClearAsync(message.From.Id);
            _logger.LogInformation("Time reset via FSM for {Date}", dateIso);
            await client.SendMessage(new ChatId(message.Chat.Id), $"Для {date:yyyy-MM-dd} теперь используется время кампании.", cancellationToken: ct);
            return;
        }

        var (success, serviceError) = await timeSetupService.SetDayTimeAsync(owner.Id, date, overrideTime.Value, ct);
        if (!success)
        {
            await client.SendMessage(new ChatId(message.Chat.Id), serviceError ?? "Не удалось обновить время дня.", cancellationToken: ct);
            return;
        }

        await _fsmStorage.ClearAsync(message.From.Id);
        _logger.LogInformation("Time updated via FSM for {Date} to {Time}", dateIso, overrideTime.Value);
        await client.SendMessage(new ChatId(message.Chat.Id), $"Для {date:yyyy-MM-dd} установлено время {overrideTime.Value:HH\\:mm}", cancellationToken: ct);
    }
}
