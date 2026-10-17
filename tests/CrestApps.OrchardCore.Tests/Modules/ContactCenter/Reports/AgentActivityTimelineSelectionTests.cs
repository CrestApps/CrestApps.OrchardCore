using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Providers;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;
using static CrestApps.OrchardCore.Tests.Modules.ContactCenter.Reports.AuditEvents;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Reports;

/// <summary>
/// The activity timeline lists its first entries, and used to build every entry of the period, detail text and all,
/// before it threw most of them away. Only the listed entries are built now; these pin that they are exactly the ones
/// building everything and keeping the first would have listed, and that the counts still cover every entry.
/// </summary>
public sealed class AgentActivityTimelineSelectionTests
{
    private static readonly DateTime _from = new(2026, 9, 21, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _to = _from.AddDays(1);

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(37)]
    [InlineData(500)]
    [InlineData(100_000)]
    public void SelectFirst_ReturnsTheFirstEntriesOfTheWholeTimeline_AndCountsEveryEntry(int limit)
    {
        // Arrange
        var (timelines, offers, calls) = BusyDay();
        var all = AgentActivityTimeline.Build(timelines, [], offers, calls, _from, _to);

        // Act
        var selection = AgentActivityTimeline.SelectFirst(timelines, [], offers, calls, _from, _to, limit);

        // Assert
        Assert.True(all.Count > 500, $"The day holds only {all.Count} entries, too few to cut.");
        Assert.Equal(
            all.Take(limit).Select(Describe),
            selection.Entries.Select(Describe));
        Assert.Equal(all.Count, selection.TotalCount);
        Assert.Equal(all.Count(entry => entry.Kind == AgentActivityKind.State), selection.StateCount);
        Assert.Equal(all.Count(entry => entry.Kind == AgentActivityKind.Offer), selection.OfferCount);
        Assert.Equal(all.Count(entry => entry.Kind == AgentActivityKind.Call), selection.CallCount);
        Assert.Equal(all.Count(entry => entry.Flagged), selection.FlaggedCount);
    }

    [Fact]
    public void Build_OrdersEntriesByStartThenRecordedTimeThenKind()
    {
        // Arrange
        var (timelines, offers, calls) = BusyDay();

        // Act
        var all = AgentActivityTimeline.Build(timelines, [], offers, calls, _from, _to);

        // Assert
        Assert.Equal(
            all.OrderBy(entry => entry.StartUtc).ThenBy(entry => entry.RecordedUtc).ThenBy(entry => entry.Kind).Select(Describe),
            all.Select(Describe));
    }

    [Fact]
    public void Report_ListsTheFirstEntries_AndCountsEveryEntryInItsSummary()
    {
        // Arrange
        var (timelines, offers, calls) = BusyDay();
        var all = AgentActivityTimeline.Build(timelines, [], offers, calls, _from, _to);
        var provider = new AgentActivityTimelineReportProvider(
            Mock.Of<IContactCenterReportingService>(),
            Mock.Of<IContactCenterReportCapabilityGuard>(),
            Mock.Of<IInteractionEventStore>(),
            Mock.Of<IAgentProfileManager>(),
            Mock.Of<IClock>(),
            Mock.Of<ILocalClock>(),
            Options.Create(new ContactCenterReportingOptions()),
            new PassThroughStringLocalizer<AgentActivityTimelineReportProvider>());

        // Act
        var document = provider.Build(
            AgentActivityTimeline.SelectFirst(timelines, [], offers, calls, _from, _to, 100),
            agentId => agentId);

        // Assert
        Assert.Equal(all.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), document.Sections[0].Metrics[0].Value);
        Assert.Equal(100, document.Sections[1].Rows.Count);
        Assert.Contains($"The first 100 of {all.Count}", document.Sections[1].Description, StringComparison.Ordinal);
    }

    private static string Describe(AgentActivityEntry entry)
        => string.Join(
            "|",
            entry.AgentId,
            entry.Kind,
            entry.Name,
            entry.Detail,
            entry.StartUtc.Ticks,
            entry.EndUtc?.Ticks,
            entry.DurationSeconds,
            entry.InteractionId,
            entry.RecordedUtc.Ticks,
            entry.Flagged);

    // Six agents cycling through a call every few minutes, with offers and call changes landing on the same instants as
    // the states, so the order falls back to the recorded time, the kind and the order the entries were made in.
    private static (IReadOnlyList<AgentStateTimeline> Timelines, InteractionEvent[] Offers, InteractionEvent[] Calls) BusyDay()
    {
        var states = new List<InteractionEvent>();
        var offers = new List<InteractionEvent>();
        var calls = new List<InteractionEvent>();

        for (var agent = 0; agent < 6; agent++)
        {
            var agentId = $"agent-{agent}";
            states.Add(State(agentId, _from.AddHours(-1), AgentPresenceStatus.Offline, AgentPresenceStatus.Available, AgentStateChangeSources.SignIn));

            for (var cycle = 0; cycle < 40; cycle++)
            {
                var start = _from.AddMinutes(cycle * 30);
                var callId = $"call-{agent}-{cycle}";

                states.Add(State(agentId, start, AgentPresenceStatus.Available, AgentPresenceStatus.Reserved, AgentStateChangeSources.Reserved, interactionId: callId, actorType: ContactCenterActorType.System));
                states.Add(State(agentId, start.AddSeconds(10), AgentPresenceStatus.Reserved, AgentPresenceStatus.Busy, AgentStateChangeSources.Accepted, interactionId: callId));

                // Every seventh cycle records the wrap-up as leaving Available, which breaks the chain and is flagged.
                states.Add(State(agentId, start.AddMinutes(12), cycle % 7 == 3 ? AgentPresenceStatus.Available : AgentPresenceStatus.Busy, AgentPresenceStatus.WrapUp, AgentStateChangeSources.WrapUpStarted, interactionId: callId));
                states.Add(State(agentId, start.AddMinutes(15), AgentPresenceStatus.WrapUp, AgentPresenceStatus.Available, AgentStateChangeSources.WorkCompleted));

                offers.Add(Offer(ContactCenterConstants.Events.OfferPresented, $"reservation-{agent}-{cycle}", callId, agentId, start));
                offers.Add(Offer(ContactCenterConstants.Events.OfferAccepted, $"reservation-{agent}-{cycle}", callId, agentId, start, start.AddSeconds(10)));
                calls.Add(Call(ContactCenterConstants.Events.CallConnected, callId, start.AddSeconds(10), agentId));
                calls.Add(Call(ContactCenterConstants.Events.CallEnded, callId, start.AddMinutes(12), agentId, durationSeconds: 710));
            }
        }

        return (AgentStateTimeline.Build(states), offers.ToArray(), calls.ToArray());
    }
}
