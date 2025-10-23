using System.Collections.Generic;
using Bot.Fsm;
using FluentAssertions;
using Xunit;

public class FsmStorageTests
{
    [Fact]
    public async Task SetThenGetReturnsSnapshot()
    {
        var storage = new InMemoryFsmStorage();
        var snapshot = new FsmSnapshot
        {
            UserId = 123,
            State = FsmState.ImportAwaitFile,
            Payload = new Dictionary<string, string> { ["key"] = "value" }
        };

        await storage.SetAsync(snapshot);

        var actual = await storage.GetAsync(snapshot.UserId);
        actual.Should().NotBeNull();
        actual!.State.Should().Be(FsmState.ImportAwaitFile);
        actual.Payload.Should().ContainKey("key").WhoseValue.Should().Be("value");
    }

    [Fact]
    public async Task ClearRemovesSnapshot()
    {
        var storage = new InMemoryFsmStorage();
        var snapshot = new FsmSnapshot
        {
            UserId = 456,
            State = FsmState.EditAwaitDate
        };

        await storage.SetAsync(snapshot);
        await storage.ClearAsync(snapshot.UserId);

        var actual = await storage.GetAsync(snapshot.UserId);
        actual.Should().BeNull();
    }

    [Fact]
    public async Task CleanupExpiredAsyncRemovesStaleSnapshots()
    {
        var storage = new InMemoryFsmStorage();
        var snapshot = new FsmSnapshot
        {
            UserId = 789,
            State = FsmState.EditAwaitText,
            UpdatedAtUtc = DateTime.UtcNow - TimeSpan.FromHours(1)
        };

        await storage.SetAsync(snapshot);
        await storage.CleanupExpiredAsync();

        var actual = await storage.GetAsync(snapshot.UserId);
        actual.Should().BeNull();
    }
}
