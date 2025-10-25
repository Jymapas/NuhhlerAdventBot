using System.Collections.Concurrent;

namespace Bot.Fsm;

public sealed class InMemoryFsmStorage : IFsmStorage
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);
    private readonly ConcurrentDictionary<long, FsmSnapshot> _storage = new();

    public Task<FsmSnapshot?> GetAsync(long userId)
    {
        if (!_storage.TryGetValue(userId, out var snapshot))
        {
            return Task.FromResult<FsmSnapshot?>(null);
        }

        if (IsExpired(snapshot))
        {
            _storage.TryRemove(userId, out _);
            return Task.FromResult<FsmSnapshot?>(null);
        }

        return Task.FromResult<FsmSnapshot?>(Clone(snapshot));
    }

    public Task SetAsync(FsmSnapshot snapshot)
    {
        var copy = Clone(snapshot);
        copy.UpdatedAtUtc = snapshot.UpdatedAtUtc == default
            ? DateTime.UtcNow
            : snapshot.UpdatedAtUtc;
        _storage.AddOrUpdate(snapshot.UserId, copy, (_, _) => copy);
        return Task.CompletedTask;
    }

    public Task ClearAsync(long userId)
    {
        _storage.TryRemove(userId, out _);
        return Task.CompletedTask;
    }

    public Task CleanupExpiredAsync()
    {
        var threshold = DateTime.UtcNow - Ttl;
        foreach (var (userId, snapshot) in _storage.ToArray())
        {
            if (snapshot.UpdatedAtUtc < threshold)
            {
                _storage.TryRemove(userId, out _);
            }
        }

        return Task.CompletedTask;
    }

    private static bool IsExpired(FsmSnapshot snapshot) => snapshot.UpdatedAtUtc < DateTime.UtcNow - Ttl;

    private static FsmSnapshot Clone(FsmSnapshot snapshot)
    {
        var payloadCopy = new Dictionary<string, string>(snapshot.Payload, StringComparer.Ordinal);
        return new FsmSnapshot
        {
            UserId = snapshot.UserId,
            State = snapshot.State,
            Payload = payloadCopy,
            UpdatedAtUtc = snapshot.UpdatedAtUtc
        };
    }
}
