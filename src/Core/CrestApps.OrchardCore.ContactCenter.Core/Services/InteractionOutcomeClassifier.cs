using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Decides the one <see cref="InteractionOutcome"/> of each interaction, so that every Contact Center report counts
/// answered, abandoned, voicemail and failed calls the same way.
/// </summary>
/// <remarks>
/// <para>
/// The interaction's status alone cannot say how a call turned out. A caller who hung up while an agent was being
/// offered the call was stored as failed until the provider's "cancelled" stopped being read as a failure, and a
/// caller sent to voicemail ends the same way one who gave up does. The event log says what happened: the routing
/// engine records <c>CallAbandoned</c> when a caller hangs up while waiting, and <c>CallSentToVoicemail</c> when it
/// sends one to voicemail. Reading the outcome from those events reports history correctly without rewriting it.
/// </para>
/// <para>The rules, in order:</para>
/// <list type="number">
/// <item>A recorded abandon is <see cref="InteractionOutcome.Abandoned"/>, whatever else the call went through: the
/// routing engine records it only for a caller who left before any agent answered, and the platform answering the
/// caller itself (to play the queue) is not an agent answering.</item>
/// <item>A call the platform sent to voicemail is <see cref="InteractionOutcome.Voicemail"/>. The interaction is
/// flagged when it is sent, and the flag survives on history whose event was lost. The platform answers the leg to
/// record the message, so the provider's answer does not make it an answered call.</item>
/// <item>Any other call an agent answered is <see cref="InteractionOutcome.Answered"/>.</item>
/// <item>A call that has not settled is <see cref="InteractionOutcome.InProgress"/>.</item>
/// <item>A settled call the provider failed is <see cref="InteractionOutcome.Failed"/>.</item>
/// <item>Any other inbound call that ended unanswered is <see cref="InteractionOutcome.Abandoned"/> (a caller who
/// hung up before reaching a queue has no abandon on record); an outbound one is
/// <see cref="InteractionOutcome.NotConnected"/>.</item>
/// </list>
/// </remarks>
public sealed class InteractionOutcomeClassifier
{
    private readonly Dictionary<string, CallEvidence> _evidence;

    private InteractionOutcomeClassifier(Dictionary<string, CallEvidence> evidence)
    {
        _evidence = evidence;
    }

    /// <summary>
    /// Gets a classifier that decides from the interactions alone, for a caller that has no event log to offer.
    /// </summary>
    public static InteractionOutcomeClassifier WithoutEvents { get; } = new([]);

    /// <summary>
    /// Gets the event types the classifier reads from the event log.
    /// </summary>
    public static IReadOnlyList<string> EvidenceEventTypes { get; } =
    [
        ContactCenterConstants.Events.CallQueued,
        ContactCenterConstants.Events.CallDequeued,
        ContactCenterConstants.Events.CallAbandoned,
        ContactCenterConstants.Events.CallSentToVoicemail,
    ];

    /// <summary>
    /// Builds a classifier from the call events recorded against the interactions it will classify.
    /// </summary>
    /// <param name="events">The events; any whose type is not one of <see cref="EvidenceEventTypes"/> is ignored.</param>
    /// <returns>The classifier.</returns>
    public static InteractionOutcomeClassifier FromEvents(IEnumerable<InteractionEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        var evidence = new Dictionary<string, CallEvidence>(StringComparer.Ordinal);

        foreach (var interactionEvent in events.Where(value => value is not null).OrderBy(value => value.OccurredUtc))
        {
            var interactionId = interactionEvent.InteractionId ?? interactionEvent.AggregateId;

            if (string.IsNullOrEmpty(interactionId) || !EvidenceEventTypes.Contains(interactionEvent.EventType, StringComparer.Ordinal))
            {
                continue;
            }

            if (!evidence.TryGetValue(interactionId, out var call))
            {
                evidence[interactionId] = call = new CallEvidence();
            }

            call.Add(interactionEvent);
        }

        return new InteractionOutcomeClassifier(evidence);
    }

    /// <summary>
    /// Loads a classifier for a set of interactions from the event log.
    /// </summary>
    /// <param name="eventStore">The event log.</param>
    /// <param name="interactions">The interactions the classifier will be asked about.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The classifier.</returns>
    public static async Task<InteractionOutcomeClassifier> LoadAsync(
        IInteractionEventStore eventStore,
        IReadOnlyCollection<Interaction> interactions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventStore);
        ArgumentNullException.ThrowIfNull(interactions);

        if (interactions.Count == 0)
        {
            return WithoutEvents;
        }

        // A call's events fall between the interaction's creation and, at the latest, a little after it ended; the
        // recording that proves a voicemail can arrive after the provider reports the hangup.
        var fromUtc = interactions.Min(interaction => interaction.CreatedUtc);
        var throughUtc = interactions.Max(interaction => interaction.EndedUtc ?? interaction.CreatedUtc).AddDays(1);

        var events = await eventStore.GetByAggregateWindowAsync(
            nameof(Interaction),
            EvidenceEventTypes,
            interactions.Select(interaction => interaction.ItemId).Where(id => !string.IsNullOrEmpty(id)),
            fromUtc,
            throughUtc,
            cancellationToken);

        return FromEvents(events);
    }

    /// <summary>
    /// Gets the outcome of an interaction.
    /// </summary>
    /// <param name="interaction">The interaction.</param>
    /// <returns>The outcome.</returns>
    public InteractionOutcome Classify(Interaction interaction)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        var call = Find(interaction);

        if (call?.AbandonedUtc is not null)
        {
            return InteractionOutcome.Abandoned;
        }

        if (call?.SentToVoicemailUtc is not null || IsFlaggedVoicemail(interaction))
        {
            return InteractionOutcome.Voicemail;
        }

        if (interaction.AnsweredUtc.HasValue)
        {
            return InteractionOutcome.Answered;
        }

        if (!interaction.IsSettled)
        {
            return InteractionOutcome.InProgress;
        }

        if (interaction.Status == InteractionStatus.Failed)
        {
            return InteractionOutcome.Failed;
        }

        return interaction.Direction == InteractionDirection.Inbound
            ? InteractionOutcome.Abandoned
            : InteractionOutcome.NotConnected;
    }

    /// <summary>
    /// Gets whether an agent answered the interaction.
    /// </summary>
    /// <param name="interaction">The interaction.</param>
    /// <returns><see langword="true"/> when the outcome is <see cref="InteractionOutcome.Answered"/>.</returns>
    public bool IsAnswered(Interaction interaction) => Classify(interaction) == InteractionOutcome.Answered;

    /// <summary>
    /// Gets whether the customer abandoned the interaction.
    /// </summary>
    /// <param name="interaction">The interaction.</param>
    /// <returns><see langword="true"/> when the outcome is <see cref="InteractionOutcome.Abandoned"/>.</returns>
    public bool IsAbandoned(Interaction interaction) => Classify(interaction) == InteractionOutcome.Abandoned;

    /// <summary>
    /// Gets whether the platform sent the interaction to voicemail.
    /// </summary>
    /// <param name="interaction">The interaction.</param>
    /// <returns><see langword="true"/> when the outcome is <see cref="InteractionOutcome.Voicemail"/>.</returns>
    public bool IsVoicemail(Interaction interaction) => Classify(interaction) == InteractionOutcome.Voicemail;

    /// <summary>
    /// Gets whether the interaction failed for a technical reason.
    /// </summary>
    /// <param name="interaction">The interaction.</param>
    /// <returns><see langword="true"/> when the outcome is <see cref="InteractionOutcome.Failed"/>.</returns>
    public bool IsFailed(Interaction interaction) => Classify(interaction) == InteractionOutcome.Failed;

    /// <summary>
    /// Gets how long an abandoning caller waited: from joining the queue to hanging up.
    /// </summary>
    /// <param name="interaction">The abandoned interaction.</param>
    /// <returns>The wait in seconds. Without an abandon on record it is the whole unanswered call, which is all the
    /// interaction itself can tell.</returns>
    public double GetWaitBeforeAbandonSeconds(Interaction interaction)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        var call = Find(interaction);

        if (call?.AbandonedUtc is { } abandonedUtc)
        {
            return call.AbandonedWaitSeconds ?? SecondsBetween(call.FirstQueuedUtc ?? interaction.CreatedUtc, abandonedUtc);
        }

        return WholeCallSeconds(interaction);
    }

    /// <summary>
    /// Gets how long a caller waited before being sent to voicemail: from joining the queue to leaving it for
    /// voicemail. The greeting and the message are not waiting.
    /// </summary>
    /// <param name="interaction">The interaction sent to voicemail.</param>
    /// <returns>The wait in seconds. Without the queue departure on record it is measured to the voicemail event, and
    /// without either it is the whole call, which is all the interaction itself can tell.</returns>
    public double GetWaitBeforeVoicemailSeconds(Interaction interaction)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        var call = Find(interaction);

        if (call?.LastDequeuedUtc is { } dequeuedUtc &&
            (call.SentToVoicemailUtc is null || dequeuedUtc <= call.SentToVoicemailUtc.Value))
        {
            return call.LastDequeuedWaitSeconds ?? SecondsBetween(call.FirstQueuedUtc ?? interaction.CreatedUtc, dequeuedUtc);
        }

        if (call?.SentToVoicemailUtc is { } sentUtc)
        {
            return SecondsBetween(call.FirstQueuedUtc ?? interaction.CreatedUtc, sentUtc);
        }

        return WholeCallSeconds(interaction);
    }

    /// <summary>
    /// Gets how long the caller waited, measured to what became of the call: to an agent answering for an answered
    /// call, to leaving the queue for voicemail for a voicemail, and to hanging up for an abandon.
    /// </summary>
    /// <param name="interaction">The interaction.</param>
    /// <returns>The wait in seconds, or zero for a call that is still in progress, failed or did not connect.</returns>
    public double GetWaitSeconds(Interaction interaction)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        return Classify(interaction) switch
        {
            InteractionOutcome.Answered => SecondsBetween(interaction.CreatedUtc, interaction.AnsweredUtc.Value),
            InteractionOutcome.Voicemail => GetWaitBeforeVoicemailSeconds(interaction),
            InteractionOutcome.Abandoned => GetWaitBeforeAbandonSeconds(interaction),
            _ => 0d,
        };
    }

    /// <summary>
    /// Gets how long an agent was connected to the caller: from the answer to the call's end, for an answered call.
    /// </summary>
    /// <param name="interaction">The interaction.</param>
    /// <returns>The connected seconds; zero for any call no agent answered. The platform answers a caller itself to
    /// record a voicemail or to play the queue, and that time is not talk time.</returns>
    public double GetTalkSeconds(Interaction interaction)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        return Classify(interaction) == InteractionOutcome.Answered &&
            interaction.EndedUtc.HasValue &&
            interaction.EndedUtc.Value >= interaction.AnsweredUtc.Value
                ? (interaction.EndedUtc.Value - interaction.AnsweredUtc.Value).TotalSeconds
                : 0d;
    }

    private CallEvidence Find(Interaction interaction)
        => !string.IsNullOrEmpty(interaction.ItemId) && _evidence.TryGetValue(interaction.ItemId, out var call) ? call : null;

    private static bool IsFlaggedVoicemail(Interaction interaction)
        => interaction.TechnicalMetadata is not null &&
            interaction.TechnicalMetadata.TryGetValue(ContactCenterConstants.Voicemail.ProjectionMetadataKey, out var value) &&
            (value is bool flagged ? flagged : bool.TryParse(value?.ToString(), out var parsed) && parsed);

    private static double WholeCallSeconds(Interaction interaction)
        => interaction.EndedUtc.HasValue ? SecondsBetween(interaction.CreatedUtc, interaction.EndedUtc.Value) : 0d;

    private static double SecondsBetween(DateTime fromUtc, DateTime toUtc)
        => Math.Max(0d, (toUtc - fromUtc).TotalSeconds);

    private sealed class CallEvidence
    {
        public DateTime? FirstQueuedUtc { get; private set; }

        public DateTime? LastDequeuedUtc { get; private set; }

        public double? LastDequeuedWaitSeconds { get; private set; }

        public DateTime? AbandonedUtc { get; private set; }

        public double? AbandonedWaitSeconds { get; private set; }

        public DateTime? SentToVoicemailUtc { get; private set; }

        // Events arrive oldest first.
        public void Add(InteractionEvent interactionEvent)
        {
            switch (interactionEvent.EventType)
            {
                case ContactCenterConstants.Events.CallQueued:
                    FirstQueuedUtc ??= interactionEvent.OccurredUtc;
                    break;
                case ContactCenterConstants.Events.CallDequeued:
                    LastDequeuedUtc = interactionEvent.OccurredUtc;
                    LastDequeuedWaitSeconds = interactionEvent.GetData<CallLifecycleEventData>()?.DurationSeconds;
                    break;
                case ContactCenterConstants.Events.CallAbandoned:
                    AbandonedUtc ??= interactionEvent.OccurredUtc;
                    AbandonedWaitSeconds ??= interactionEvent.GetData<CallLifecycleEventData>()?.DurationSeconds;
                    break;
                case ContactCenterConstants.Events.CallSentToVoicemail:
                    SentToVoicemailUtc ??= interactionEvent.OccurredUtc;
                    break;
            }
        }
    }
}
