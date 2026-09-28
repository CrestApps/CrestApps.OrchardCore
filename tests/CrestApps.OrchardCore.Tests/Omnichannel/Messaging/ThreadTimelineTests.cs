using System.Text.Encodings.Web;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;

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

    [Fact]
    public void DescribeTransfer_WithNoSenderOrActor_SaysOnlyWhereItWent()
    {
        var sentence = Describe(new MessagingConversationEvent { ToAgentId = "agent-b", ToName = "Bea" });

        Assert.Equal("Transferred to Bea", sentence);
    }

    // An agent who hands on their own conversation is named once, not as "Ann transferred this conversation from Ann".
    [Fact]
    public void DescribeTransfer_MadeByTheSender_NamesThemOnceAsTheSender()
    {
        var sentence = Describe(new MessagingConversationEvent
        {
            FromAgentId = "agent-a",
            FromName = "Ann",
            ActorAgentId = "agent-a",
            ActorName = "Ann",
            ToAgentId = "agent-b",
            ToName = "Bea",
        });

        Assert.Equal("Transferred from Ann to Bea", sentence);
    }

    [Fact]
    public void DescribeTransfer_MadeByASupervisor_NamesThemAsWellAsTheSender()
    {
        var sentence = Describe(new MessagingConversationEvent
        {
            FromAgentId = "agent-a",
            FromName = "Ann",
            ActorAgentId = "agent-s",
            ActorName = "Sam",
            ToAgentId = "agent-b",
            ToName = "Bea",
        });

        Assert.Equal("Sam transferred this conversation from Ann to Bea", sentence);
    }

    [Fact]
    public void DescribeTransfer_OfAnUnclaimedConversationBySomebody_NamesWhoMovedIt()
    {
        var sentence = Describe(new MessagingConversationEvent { ActorAgentId = "agent-s", ActorName = "Sam", ToAgentId = "agent-b", ToName = "Bea" });

        Assert.Equal("Sam transferred this conversation to Bea", sentence);
    }

    // A queue is named as one, so "to Billing" is not read as a person called Billing (it once read "the Billing team").
    [Fact]
    public void DescribeTransfer_ToAQueue_NamesItAsAQueue()
    {
        var sentence = Describe(new MessagingConversationEvent { FromName = "Ann", ToQueueId = "queue-1", ToName = "Billing" });

        Assert.Equal("Transferred from Ann to the Billing queue", sentence);
    }

    [Fact]
    public void DescribeTransfer_WithoutTheDestinationsName_FallsBackToGenericWording()
    {
        Assert.Equal("Transferred to another agent", Describe(new MessagingConversationEvent { ToAgentId = "agent-b" }));
        Assert.Equal("Transferred to the unknown queue", Describe(new MessagingConversationEvent { ToQueueId = "queue-1", ToName = " " }));
    }

    // The queue's wording is formatted as plain text and then encoded as an argument of the sentence, never twice.
    [Fact]
    public void DescribeTransfer_EncodesTheNamesOnce()
    {
        var sentence = Describe(new MessagingConversationEvent { FromName = "Ann <Lead>", ToQueueId = "queue-1", ToName = "R&D" });

        Assert.Equal("Transferred from Ann &lt;Lead&gt; to the R&amp;D queue", sentence);
    }

    // Renders the sentence the way the view writes it.
    private static string Describe(MessagingConversationEvent entry)
    {
        using var writer = new StringWriter();

        ThreadTimeline.DescribeTransfer(entry, new PassThroughHtmlLocalizer()).WriteTo(writer, HtmlEncoder.Default);

        return writer.ToString();
    }

    private static OmnichannelMessage Message(string id, int minutes)
        => new() { Id = id, CreatedUtc = _start.AddMinutes(minutes) };

    private static MessagingConversationEvent Event(string note, int minutes)
        => new() { Note = note, OccurredUtc = _start.AddMinutes(minutes) };

    private static string Label(ThreadTimelineItem item)
        => item.Message?.Id ?? item.Event.Note;

    // Keeps the arguments of a sentence apart from it, as the view localizer does, so they are encoded when written.
    private sealed class PassThroughHtmlLocalizer : IHtmlLocalizer
    {
        public LocalizedHtmlString this[string name] => new(name, name);

        public LocalizedHtmlString this[string name, params object[] arguments] => new(name, name, false, arguments);

        public LocalizedString GetString(string name) => new(name, name);

        public LocalizedString GetString(string name, params object[] arguments) => new(name, string.Format(name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
