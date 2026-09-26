using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Tells which measured call legs belonged to a call an agent actually talked on.
/// </summary>
/// <remarks>
/// <para>
/// A provider measures every leg it carries, including legs no agent ever spoke on: a caller sent to voicemail, who
/// heard the greeting and left a message; a caller who hung up while an agent's phone was still ringing; a call only the
/// AI voice agent handled. Those legs carried a greeting, ringback or one-way audio, not a conversation, so what they
/// measured says nothing about the agent whose leg was offered the call.
/// </para>
/// <para>
/// An interaction's outcome is read with <see cref="InteractionOutcomeClassifier"/>, so a call counts as a
/// conversation exactly when every other Contact Center report counts it as answered, and only when an agent was on
/// it. A leg of a call the contact center did not route, such as an extension call between two agents, has no
/// interaction to ask and is kept, as is one whose interaction is no longer on record.
/// </para>
/// </remarks>
public sealed class CallQualityAgentConversations
{
    private static readonly CallQualityAgentConversations _none = new(new Dictionary<string, Interaction>(StringComparer.Ordinal), InteractionOutcomeClassifier.WithoutEvents);

    private readonly Dictionary<string, Interaction> _interactions;
    private readonly InteractionOutcomeClassifier _outcomes;

    private CallQualityAgentConversations(Dictionary<string, Interaction> interactions, InteractionOutcomeClassifier outcomes)
    {
        _interactions = interactions;
        _outcomes = outcomes;
    }

    /// <summary>
    /// Loads what is needed to tell, for a set of records, whether each belonged to an agent conversation.
    /// </summary>
    /// <param name="interactionStore">The interactions.</param>
    /// <param name="eventStore">The event log, from which each interaction's outcome is read.</param>
    /// <param name="records">The records that will be asked about.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The lookup.</returns>
    public static async Task<CallQualityAgentConversations> LoadAsync(
        IInteractionStore interactionStore,
        IInteractionEventStore eventStore,
        IEnumerable<CallQualityRecord> records,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interactionStore);
        ArgumentNullException.ThrowIfNull(eventStore);
        ArgumentNullException.ThrowIfNull(records);

        var interactionIds = records
            .Select(record => record?.InteractionId)
            .Where(interactionId => !string.IsNullOrEmpty(interactionId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (interactionIds.Length == 0)
        {
            return _none;
        }

        var interactions = (await interactionStore.GetAsync(interactionIds, cancellationToken))
            .Where(interaction => !string.IsNullOrEmpty(interaction?.ItemId))
            .DistinctBy(interaction => interaction.ItemId, StringComparer.Ordinal)
            .ToDictionary(interaction => interaction.ItemId, StringComparer.Ordinal);

        if (interactions.Count == 0)
        {
            return _none;
        }

        var outcomes = await InteractionOutcomeClassifier.LoadAsync(eventStore, interactions.Values, cancellationToken);

        return new CallQualityAgentConversations(interactions, outcomes);
    }

    /// <summary>
    /// Gets whether the record's leg belonged to a call an agent talked on.
    /// </summary>
    /// <param name="record">The record.</param>
    /// <returns><see langword="true"/> when an agent had a conversation on the call, or when the call is not one the
    /// contact center can tell about.</returns>
    public bool HadAgentConversation(CallQualityRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (string.IsNullOrEmpty(record.InteractionId) ||
            !_interactions.TryGetValue(record.InteractionId, out var interaction))
        {
            return true;
        }

        return !string.IsNullOrEmpty(interaction.AgentId) &&
            _outcomes.Classify(interaction) == InteractionOutcome.Answered;
    }
}
