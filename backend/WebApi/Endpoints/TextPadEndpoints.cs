using Application.Validation;
using Infrastructure.Repositories;
using Application.Abstractions;
using Microsoft.AspNetCore.RateLimiting;

namespace WebApi.Endpoints;

public static class TextPadEndpoints
{
    public static void MapTextPad(this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/textpad").RequireRateLimiting("default");

        grp.MapGet("/", async (HttpRequest req, ITelegramAuth tg, INoteRepository repo, CancellationToken ct) =>
        {
            var init = req.Headers["X-Telegram-Init-Data"].ToString();
            var (userId, _, _) = tg.ValidateInitData(init);

            int skip = int.TryParse(req.Query["skip"], out var s) ? s : 0;
            int take = int.TryParse(req.Query["take"], out var t) ? Math.Min(t, 100) : 20;

            var list = await repo.ListAsync(userId, skip, take, ct);
            return Results.Ok(list);
        });

        grp.MapPost("/", [EnableRateLimiting("post-strict")] async (HttpRequest req, ITelegramAuth tg, INoteRepository repo, NoteInput input, CancellationToken ct) =>
        {
            var init = req.Headers["X-Telegram-Init-Data"].ToString();
            var (userId, username, _) = tg.ValidateInitData(init);

            var validator = new NoteInputValidator();
            var vr = await validator.ValidateAsync(input, ct);
            if (!vr.IsValid) return Results.ValidationProblem(vr.ToDictionary());

            var text = TextNormalizer.Normalize(input.Text);
            var note = await repo.UpsertAsync(userId, username, text, ct);
            return Results.Ok(note);
        });

        grp.MapGet("/{id:long}", async (long id, HttpRequest req, ITelegramAuth tg, INoteRepository repo, CancellationToken ct) =>
        {
            var init = req.Headers["X-Telegram-Init-Data"].ToString();
            var (userId, _, _) = tg.ValidateInitData(init);

            var note = await repo.GetAsync(id, userId, ct);
            return note is null ? Results.NotFound() : Results.Ok(note);
        });

        grp.MapDelete("/{id:long}", async (long id, HttpRequest req, ITelegramAuth tg, INoteRepository repo, CancellationToken ct) =>
        {
            var init = req.Headers["X-Telegram-Init-Data"].ToString();
            var (userId, _, _) = tg.ValidateInitData(init);

            var ok = await repo.DeleteAsync(id, userId, ct);
            return ok ? Results.NoContent() : Results.NotFound();
        });
    }
}
