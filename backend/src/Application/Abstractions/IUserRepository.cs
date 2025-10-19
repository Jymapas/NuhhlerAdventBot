using Domain.Users;

namespace Application.Abstractions;

public interface IUserRepository
{
    Task<User?> GetByTelegramIdAsync(long telegramId, CancellationToken ct);
    Task<User> EnsureAsync(long telegramId, string? username, string? firstName, CancellationToken ct);
    Task<User?> GetByIdAsync(long id, CancellationToken ct);
}
