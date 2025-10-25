using System;
using Application.Abstractions;
using Domain.Users;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public sealed class UserRepository(AppDbContext dbContext) : IUserRepository
{
    public async Task<User?> GetByTelegramIdAsync(long telegramId, CancellationToken ct) =>
        await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(x => x.TelegramId == telegramId, ct);

    public async Task<User?> GetByIdAsync(long id, CancellationToken ct) =>
        await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<User> EnsureAsync(long telegramId, string? username, string? firstName, CancellationToken ct)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(x => x.TelegramId == telegramId, ct);
        if (user is null)
        {
            user = new User
            {
                TelegramId = telegramId,
                Username = username,
                FirstName = firstName,
                CreatedAtUtc = DateTime.UtcNow,
                LastSeenAtUtc = DateTime.UtcNow
            };

            await dbContext.Users.AddAsync(user, ct);
        }
        else
        {
            user.Username = username ?? user.Username;
            user.FirstName = firstName ?? user.FirstName;
            user.LastSeenAtUtc = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(ct);
        return user;
    }

    public async Task<User?> GetByUsernameAsync(string username, CancellationToken ct)
    {
        var normalized = username.ToLowerInvariant();
        return await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(
            x => x.Username != null && x.Username.ToLower() == normalized,
            ct);
    }
}
