using System;
using System.IO;
using Application.Abstractions;
using Application.Import;
using Bot.Commands;
using Bot.Fsm;
using Bot.Handlers.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Bot.Handlers;

public sealed class TemplateCommandHandler : HandlerBase, ICommandHandler
{
    private readonly IServiceScopeFactory _scopeFactory;

    public TemplateCommandHandler(
        IFsmStorage fsmStorage,
        IConfiguration configuration,
        ILogger<TemplateCommandHandler> logger,
        IServiceScopeFactory scopeFactory)
        : base(fsmStorage, configuration, logger)
    {
        _scopeFactory = scopeFactory;
    }

    public bool CanHandle(string command) =>
        string.Equals(command, "/template", StringComparison.OrdinalIgnoreCase);

    public async Task HandleAsync(ITelegramBotClient client, Update update, string command, string? args, CancellationToken cancellationToken)
    {
        if (update.Message?.From is null)
            return;

        var telegramUser = update.Message.From;

        using var scope = _scopeFactory.CreateScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var adventRepository = scope.ServiceProvider.GetRequiredService<IAdventRepository>();
        var templateGenerator = scope.ServiceProvider.GetRequiredService<ITemplateGenerator>();

        EnsureOwner(telegramUser.Id, telegramUser.Username);

        var owner = await userRepository.EnsureAsync(telegramUser.Id, telegramUser.Username, telegramUser.FirstName, cancellationToken);
        var campaign = await adventRepository.GetActiveOrDraftByOwnerAsync(owner.Id, cancellationToken);

        if (campaign is null)
        {
            await ReplyAsync(
                client,
                GetChatId(update),
                "Сначала создайте кампанию через /new_advent.",
                cancellationToken);
            return;
        }

        var year = campaign.StartDate.Year;
        var csv = await templateGenerator.GenerateCsvAsync(year, cancellationToken);
        var xlsx = await templateGenerator.GenerateXlsxAsync(year, cancellationToken);

        var chatId = new ChatId(update.Message.Chat.Id);

        await using (var csvStream = new MemoryStream(csv.data, writable: false))
        {
            await client.SendDocument(
                chatId,
                InputFile.FromStream(csvStream, csv.fileName),
                caption: "Вот шаблон CSV",
                cancellationToken: cancellationToken);
        }

        await using (var xlsxStream = new MemoryStream(xlsx.data, writable: false))
        {
            await client.SendDocument(
                chatId,
                InputFile.FromStream(xlsxStream, xlsx.fileName),
                caption: "Вот шаблон XLSX",
                cancellationToken: cancellationToken);
        }
    }
}
