namespace Bot.Fsm;

public interface IFsmStorage
{
    Task<FsmSnapshot?> GetAsync(long userId);
    Task SetAsync(FsmSnapshot snapshot);
    Task ClearAsync(long userId);
    Task CleanupExpiredAsync();
}
