using FluentAssertions;
using SignalRRealtime.Api.Services;
using Xunit;

namespace SignalRRealtime.Tests;

public sealed class RealtimeUnitTests
{
    [Fact]
    public void NormalizeRoom_Trims_Lowercases_And_Replaces_Spaces()
    {
        InputSanitizer.NormalizeRoom(" Trading Floor ").Should().Be("trading-floor");
    }

    [Fact]
    public void NormalizeRoom_Rejects_Unsafe_Characters()
    {
        var act = () => InputSanitizer.NormalizeRoom("../admin");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void NormalizeRoom_Rejects_Names_Over_The_Limit_Instead_Of_Renaming_The_Room()
    {
        var act = () => InputSanitizer.NormalizeRoom(new string('r', 41));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void NormalizeMessage_Rejects_Text_Over_The_Limit()
    {
        var act = () => InputSanitizer.NormalizeMessage(new string('x', InputSanitizer.MaxMessageLength + 1));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void PresenceTracker_Joins_Leaves_And_Cleans_Up_On_Disconnect()
    {
        var tracker = new RoomPresenceTracker();

        tracker.Join("lobby", "conn-1", "Ada").Should().Be(1);
        tracker.Join("lobby", "conn-2", "Grace").Should().Be(2);
        tracker.GetUsers("lobby").Should().Equal("Ada", "Grace");
        tracker.IsMember("lobby", "conn-1").Should().BeTrue();

        tracker.Leave("lobby", "conn-2").Should().Be(1);
        var changes = tracker.LeaveAll("conn-1");

        changes.Should().ContainSingle();
        changes[0].Room.Should().Be("lobby");
        changes[0].UserCount.Should().Be(0);
        tracker.GetUserCount("lobby").Should().Be(0);
    }

    [Fact]
    public void QuoteStore_Starts_With_The_Dashboard_Symbols_And_Updates_Prices()
    {
        var store = new QuoteStore();
        var before = store.GetSnapshot();

        before.Select(quote => quote.Symbol)
            .Should().Equal("AAPL", "AMZN", "MSFT", "NVDA");

        var after = store.ApplyRandomWalk(new Random(42));

        after.Should().HaveCount(4);
        after.Should().OnlyContain(quote => quote.Price > 0);
        after.Select(quote => quote.UpdatedUtc)
            .Should().OnlyContain(time => time <= DateTime.UtcNow.AddSeconds(1));
    }
}
