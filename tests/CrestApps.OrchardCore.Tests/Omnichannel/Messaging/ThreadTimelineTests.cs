using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

public sealed class ThreadTimelineTests
{
    private static readonly DateTime _start = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Merge_PlacesATransferBetweenTheMessagesItHappenedBetween()
    {
        var messages = new[] { Message("m1", 0), Message("m2", 10) };
        var events = new[] { Event("moved", 5) };

        var items = ThreadTimeline.Merge(messages, events);

        Assert.Equal(["m1", "moved", "m2"], items.Select(Label));
    }

    [Fact]
    public void Merge_KeepsAMessageAheadOfATransferRecordedAtTheSameMoment()
    {
        var messages = new[] { Message("m1", 5) };
        var events = new[] { Event("moved", 5) };

        var items = ThreadTimeline.Merge(messages, events);

        Assert.Equal(["m1", "moved"], items.Select(Label));
    }

    [Fact]
    public void Merge_ShowsTransfersEvenWhenTheThreadHasNoMessages()
    {
        var items = ThreadTimeline.Merge([], [Event("moved", 5), Event("again", 1)]);

        Assert.Equal(["again", "moved"], items.Select(Label));
    }

    [Fact]
    public void ForPage_OnTheNewestPageWithEarlierMessages_LeavesOutTransfersOlderThanThePage()
    {
        // An older transfer belongs with the earlier messages, shown when the agent loads them.
        var messages = new[] { Message("m1", 10), Message("m2", 20) };
        var history = new[] { Event("old", 5), Event("recent", 15) };

        var events = ThreadTimeline.ForPage(history, messages, beforeUtc: null, hasEarlierMessages: true);

        Assert.Equal(["recent"], events.Select(entry => entry.Note));
    }

    [Fact]
    public void ForPage_OnTheOldestPage_IncludesEveryTransferUpToTheNextPage()
    {
        var messages = new[] { Message("m1", 10) };
        var history = new[] { Event("first", 1), Event("second", 12), Event("after", 30) };

        var events = ThreadTimeline.ForPage(history, messages, beforeUtc: _start.AddMinutes(20), hasEarlierMessages: false);

        Assert.Equal(["first", "second"], events.Select(entry => entry.Note));
    }

    [Fact]
    public void ForPage_WithNoHistory_ReturnsNothing()
    {
        Assert.Empty(ThreadTimeline.ForPage(null, [Message("m1", 1)], beforeUtc: null, hasEarlierMessages: false));
    }

    private static OmnichannelMessage Message(string id, int minutes)
        => new() { Id = id, CreatedUtc = _start.AddMinutes(minutes) };

    private static MessagingConversationEvent Event(string note, int minutes)
        => new() { Note = note, OccurredUtc = _start.AddMinutes(minutes) };

    private static string Label(ThreadTimelineItem item)
        => item.Message?.Id ?? item.Event.Note;
}
