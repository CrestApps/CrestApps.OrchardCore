using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Decides whether a poor call is one of a run, and raises an alert when it is.
/// </summary>
public interface ICallQualityAlertService
{
    /// <summary>
    /// Looks at the agent's recent calls after one was recorded, and raises an alert when too many rated poor.
    /// </summary>
    /// <param name="record">The call just recorded.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task EvaluateAsync(CallQualityRecord record, CancellationToken cancellationToken = default);
}

/// <summary>
/// Raises <see cref="ContactCenterConstants.Events.CallQualityAlertRaised"/> when an agent's recent calls keep rating
/// poor.
/// </summary>
/// <remarks>
/// One poor call is noise: a customer on a train, a single dropped packet burst. Several in a row from the same agent
/// is a headset, a network or a machine that will keep failing until somebody fixes it. The alert is an event, so
/// supervisors are told in real time and a workflow can act on it, and it is raised at most once per agent in each
/// alert window however many more poor calls follow.
/// </remarks>
public sealed class CallQualityAlertService : ICallQualityAlertService
{
    /// <summary>
    /// How many of the agent's most recent calls are looked at.
    /// </summary>
    public const int RecentCallCount = 5;

    /// <summary>
    /// How many of those must have rated poor to raise an alert.
    /// </summary>
    public const int PoorCallThreshold = 3;

    /// <summary>
    /// How far back a call still counts as recent, and how long an alert stands before another is raised.
    /// </summary>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(60);

    private readonly ICallQualityRecordStore _recordStore;
    private readonly IInteractionStore _interactionStore;
    private readonly IInteractionEventStore _eventStore;
    private readonly IContactCenterEventPublisher _publisher;

    public CallQualityAlertService(
        ICallQualityRecordStore recordStore,
        IInteractionStore interactionStore,
        IInteractionEventStore eventStore,
        IContactCenterEventPublisher publisher)
    {
        _recordStore = recordStore;
        _interactionStore = interactionStore;
        _eventStore = eventStore;
        _publisher = publisher;
    }

    public async Task EvaluateAsync(CallQualityRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (record.Rating != CallQualityRating.Poor ||
            string.IsNullOrEmpty(record.AgentId) ||
            !IsAgentSide(record))
        {
            return;
        }

        // The record just written is not yet committed, so it is not in the query; it is added here instead.
        var earlier = await _recordStore.GetRecentForAgentAsync(
            record.AgentId,
            record.ObservedUtc - Window,
            RecentCallCount * 2,
            cancellationToken);

        // Records kept before a rating rule changed are read the way the rules read them now.
        var candidates = earlier
            .Where(candidate => candidate.ItemId != record.ItemId && IsAgentSide(candidate))
            .Select(CallQualityRecordFigures.Apply)
            .Prepend(record)
            .ToArray();

        // A leg of a call no agent talked on, such as one sent to voicemail or abandoned before anybody answered,
        // measured the platform's greeting or the caller's line, never the agent.
        var conversations = await CallQualityAgentConversations.LoadAsync(_interactionStore, _eventStore, candidates, cancellationToken);

        if (!conversations.HadAgentConversation(record))
        {
            return;
        }

        // A leg both the soft phone and the provider measured is one call, counted once.
        var recent = candidates
            .Where(conversations.HadAgentConversation)
            .DistinctBy(candidate => candidate.ProviderCallControlId, StringComparer.Ordinal)
            .Take(RecentCallCount)
            .ToArray();

        var poor = recent.Where(candidate => candidate.Rating == CallQualityRating.Poor).ToArray();

        if (poor.Length < PoorCallThreshold)
        {
            return;
        }

        var notification = new CallQualityAlertNotification
        {
            AgentId = record.AgentId,
            UserId = record.UserId,
            PoorCallCount = poor.Length,
            RecentCallCount = recent.Length,
            LikelyCause = CallQualityCauseClassifier.ClassifyMostLikely(poor).ToString(),
            InteractionId = record.InteractionId,
            ObservedUtc = record.ObservedUtc,
        };

        var interactionEvent = new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.CallQualityAlertRaised,
            InteractionId = record.InteractionId,
            AggregateType = nameof(AgentProfile),
            AggregateId = record.AgentId,
            SourceComponent = ContactCenterConstants.Components.Voice,
            OccurredUtc = record.ObservedUtc,

            // One alert per agent per window: the key names the window the call ended in.
            IdempotencyKey = $"call-quality-alert:{record.AgentId}:{record.ObservedUtc.Ticks / Window.Ticks}",
        };

        interactionEvent.SetData(notification);

        await _publisher.PublishAsync(interactionEvent, cancellationToken);
    }

    // The agent's own side of a call: what their soft phone measured, or the provider's measurement of their leg.
    // A customer on a poor line says nothing about the agent.
    private static bool IsAgentSide(CallQualityRecord record)
        => record.Source == CallQualitySource.Browser || record.LegRole == CallPartyRole.Agent;
}
