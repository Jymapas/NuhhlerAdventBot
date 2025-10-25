using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bot.Fsm;
using Domain.Advent;
using FluentAssertions;
using Xunit;

public class EditStateFlowTests
{
    [Fact]
    public async Task EditingTextUpdatesDayAndClearsState()
    {
        var repository = new FakeRepository();
        var storage = new InMemoryFsmStorage();

        var snapshot = new FsmSnapshot
        {
            UserId = 1001,
            State = FsmState.EditAwaitText,
            Payload = new Dictionary<string, string>
            {
                ["campaignId"] = repository.Campaign.Id.ToString(),
                ["date"] = "2025-12-05"
            }
        };

        await storage.SetAsync(snapshot);

        await EditTextAsync(repository, storage, 1001, "Новый текст дня");

        var updated = repository.GetDay(new DateOnly(2025, 12, 5));
        updated.Should().NotBeNull();
        updated!.Text.Should().Be("Новый текст дня");
        (await storage.GetAsync(1001)).Should().BeNull();
    }

    private static async Task EditTextAsync(FakeRepository repository, IFsmStorage storage, long userId, string text)
    {
        var snapshot = await storage.GetAsync(userId) ?? throw new InvalidOperationException();
        snapshot.State.Should().Be(FsmState.EditAwaitText);

        var payload = snapshot.Payload ?? throw new InvalidOperationException();
        if (!payload.TryGetValue("date", out var dateValue))
        {
            throw new InvalidOperationException("Missing date in FSM payload");
        }

        var date = DateOnly.Parse(dateValue);
        var day = repository.GetDay(date) ?? new AdventDay
        {
            CampaignId = repository.Campaign.Id,
            Date = date,
            Text = string.Empty
        };

        day.Text = text.Trim();
        repository.SetDay(day);
        await storage.ClearAsync(userId);
    }

    private sealed class FakeRepository
    {
        private readonly Dictionary<DateOnly, AdventDay> _days = new();

        public AdventCampaign Campaign { get; }

        public FakeRepository()
        {
            Campaign = new AdventCampaign
            {
                Id = 42,
                OwnerUserId = 1,
                StartDate = new DateOnly(2025, 12, 1),
                EndDate = new DateOnly(2025, 12, 31),
                DefaultSendTime = new TimeOnly(9, 0),
                Status = CampaignStatus.Draft,
                RecipientStatus = RecipientStatus.Pending,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
                Days = new List<AdventDay>()
            };

            SetDay(new AdventDay
            {
                CampaignId = Campaign.Id,
                Date = new DateOnly(2025, 12, 5),
                Text = "Старый текст"
            });
        }

        public AdventDay? GetDay(DateOnly date)
        {
            _days.TryGetValue(date, out var day);
            return day;
        }

        public void SetDay(AdventDay day)
        {
            _days[day.Date] = day;
        }
    }
}
