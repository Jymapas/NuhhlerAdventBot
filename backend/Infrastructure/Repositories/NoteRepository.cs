using Application.Abstractions;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public interface INoteRepository
{
    Task<Note> UpsertAsync(long userId, string username, string text, CancellationToken ct);
    Task<Note?> GetAsync(long id, long userId, CancellationToken ct);
    Task<List<Note>> ListAsync(long userId, int skip, int take, CancellationToken ct);
    Task<bool> DeleteAsync(long id, long userId, CancellationToken ct);
}

public class NoteRepository(AppDbContext db) : INoteRepository
{
    public async Task<Note> UpsertAsync(long userId, string username, string text, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var note = new Note { UserId = userId, Username = username, Text = text, CreatedAt = now, UpdatedAt = now };
        db.Notes.Add(note);
        await db.SaveChangesAsync(ct);
        return note;
    }

    public Task<Note?> GetAsync(long id, long userId, CancellationToken ct) =>
        db.Notes.AsNoTracking().FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, ct);

    public Task<List<Note>> ListAsync(long userId, int skip, int take, CancellationToken ct) =>
        db.Notes.AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.UpdatedAt)
            .Skip(skip).Take(take)
            .ToListAsync(ct);

    public async Task<bool> DeleteAsync(long id, long userId, CancellationToken ct)
    {
        var n = await db.Notes.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
        if (n is null) return false;
        db.Remove(n);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
