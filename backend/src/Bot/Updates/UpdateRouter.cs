using System.Globalization;
using System.IO;
using System.Linq;
using Bot.Callbacks;
using Bot.Commands;
using Bot.Fsm;
using Application.Abstractions;
using Application.Import;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InputFiles;

namespace Bot.Updates;

public sealed class UpdateRouter : IUpdateRouter
{
    private readonly ILogger<UpdateRouter> _logger;
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly ICallbackDispatcher _callbackDispatcher;
    private readonly IFsmStorage _fsmStorage;
    private readonly IServiceScopeFactory _scopeFactory;

    public UpdateRouter(
        ILogger<UpdateRouter> logger,
        ICommandDispatcher commandDispatcher,
        ICallbackDispatcher callbackDispatcher,
        IFsmStorage fsmStorage,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _commandDispatcher = commandDispatcher;
        _callbackDispatcher = callbackDispatcher;
        _fsmStorage = fsmStorage;
        _scopeFactory = scopeFactory;
    }

    public async Task HandleAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        switch (update.Type)
        {
            case UpdateType.Message when update.Message is { Text: { } text }:
            {
                if (!text.TrimStart().StartsWith("/", StringComparison.Ordinal))
                {
                    return;
                }

                var command = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries)[0];
                var user = update.Message!.From;
                _logger.LogInformation("Command {Command} from user {UserId} ({Username})", command, user?.Id, user?.Username);

                await _commandDispatcher.DispatchAsync(botClient, update, cancellationToken);
                break;
            }

            case UpdateType.Message when update.Message is { Document: not null }:
            {
                await HandleImportFileAsync(botClient, update.Message, cancellationToken);
                break;
            }

            case UpdateType.CallbackQuery when update.CallbackQuery is { } callback:
            {
                var user = callback.From;
                _logger.LogInformation("Callback {Data} from user {UserId} ({Username})", callback.Data, user.Id, user.Username);

                await _callbackDispatcher.DispatchAsync(botClient, callback, cancellationToken);
                break;
            }

            case UpdateType.MyChatMember when update.MyChatMember is { } chatMember:
            {
                var user = chatMember.From;
                _logger.LogInformation("Chat member update: status {Status}; user {UserId} ({Username})",
                    chatMember.NewChatMember.Status, user.Id, user.Username);
                break;
            }
        }
    }

    private async Task HandleImportFileAsync(ITelegramBotClient client, Message message, CancellationToken ct)
    {
        if (message.Document is null || message.From is null)
        {
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
}
