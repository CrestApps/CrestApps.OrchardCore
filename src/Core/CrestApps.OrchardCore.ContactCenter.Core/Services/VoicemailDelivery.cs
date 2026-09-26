using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Decides where a voicemail is delivered: to one agent's personal inbox, or to a queue's shared voicemail box.
/// </summary>
/// <remarks>
/// <para>
/// The decision is made in two steps, and both live here so every path to voicemail makes it the same way. When the
/// call arrives (or a menu sends the caller to voicemail), <see cref="StampMailbox"/> records on the interaction where
/// the entry point wants a message the call has no agent for to go. When the call is actually sent to voicemail,
/// <see cref="Resolve"/> reads that intent back together with who the call is for.
/// </para>
/// <para>
/// A message that is for a specific agent always goes to that agent, whatever the entry point says: the agent who let
/// an offered call ring out, and the agent a personal line belongs to. Only a message with no agent of its own goes to
/// the line's mailbox.
/// </para>
/// </remarks>
public static class VoicemailDelivery
{
    /// <summary>
    /// Records on the interaction where the entry point delivers a message the call has no agent for. A value already
    /// on the interaction is kept, so the mailbox the call arrived with is not re-derived from a changed entry point.
    /// </summary>
    /// <param name="interaction">The interaction to stamp.</param>
    /// <param name="entryPoint">The entry point the call came in on.</param>
    /// <param name="queueId">The queue the call is routed to, when known; the entry point's target queue otherwise.</param>
    /// <returns><see langword="true"/> when the interaction was changed.</returns>
    public static bool StampMailbox(Interaction interaction, ContactCenterEntryPoint entryPoint, string queueId = null)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        // A personal line's messages belong to its agent, so it has no mailbox of its own to record.
        if (entryPoint is null || entryPoint.TargetType == EntryPointTargetType.Agent)
        {
            return false;
        }

        interaction.TechnicalMetadata ??= new Dictionary<string, object>();

        if (interaction.TechnicalMetadata.ContainsKey(ContactCenterConstants.Voicemail.SharedMailboxQueueMetadataKey) ||
            interaction.TechnicalMetadata.ContainsKey(ContactCenterConstants.Voicemail.MailboxAgentMetadataKey))
        {
            return false;
        }

        if (entryPoint.VoicemailDestination == EntryPointVoicemailDestination.QueueSharedBox)
        {
            var sharedQueueId = IsRealQueue(queueId) ? queueId : entryPoint.TargetQueueId;

            if (!IsRealQueue(sharedQueueId))
            {
                return false;
            }

            interaction.TechnicalMetadata[ContactCenterConstants.Voicemail.SharedMailboxQueueMetadataKey] = sharedQueueId;

            return true;
        }

        if (string.IsNullOrWhiteSpace(entryPoint.VoicemailRecipientAgentId))
        {
            return false;
        }

        interaction.TechnicalMetadata[ContactCenterConstants.Voicemail.MailboxAgentMetadataKey] = entryPoint.VoicemailRecipientAgentId;

        return true;
    }

    /// <summary>
    /// Decides who a message left on the interaction is for.
    /// </summary>
    /// <param name="interaction">The interaction being sent to voicemail.</param>
    /// <param name="offeredAgentId">The agent whose offer is being sent to voicemail, when an offer rang out.</param>
    /// <returns>The recipient: an agent, a queue's shared box, or nobody.</returns>
    public static VoicemailRecipient Resolve(Interaction interaction, string offeredAgentId = null)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        // The agent who let an offered call ring out is the one the caller was trying to reach.
        if (!string.IsNullOrWhiteSpace(offeredAgentId))
        {
            return VoicemailRecipient.ForAgent(offeredAgentId);
        }

        if (ReadString(interaction, ContactCenterConstants.DirectRouting.TargetAgentMetadataKey) is { } directAgentId)
        {
            return VoicemailRecipient.ForAgent(directAgentId);
        }

        // The box of the queue the caller was actually waiting in, which a menu or an overflow can make a different
        // queue from the one the line targets. The queue the call arrived with covers a caller who never reached one.
        if (ReadString(interaction, ContactCenterConstants.Voicemail.SharedMailboxQueueMetadataKey) is { } mailboxQueueId)
        {
            return VoicemailRecipient.ForSharedQueue(IsRealQueue(interaction.QueueId) ? interaction.QueueId : mailboxQueueId);
        }

        if (ReadString(interaction, ContactCenterConstants.Voicemail.MailboxAgentMetadataKey) is { } mailboxAgentId)
        {
            return VoicemailRecipient.ForAgent(mailboxAgentId);
        }

        return VoicemailRecipient.None;
    }

    /// <summary>
    /// Reads the queue whose shared box a message on the interaction was delivered to.
    /// </summary>
    /// <param name="interaction">The interaction.</param>
    /// <returns>The queue identifier, or <see langword="null"/> when the message was not delivered to a shared box.</returns>
    public static string GetSharedQueueId(Interaction interaction)
        => interaction is null
            ? null
            : ReadString(interaction, ContactCenterConstants.Voicemail.SharedQueueMetadataKey);

    // The direct-routing and campaign queues are never stored and have no team to share a box with.
    private static bool IsRealQueue(string queueId)
        => !string.IsNullOrWhiteSpace(queueId) &&
            !ContactCenterConstants.IsDirectRoutingQueue(queueId) &&
            !ContactCenterConstants.IsCampaignQueue(queueId);

    // Identifiers are written as strings, and a string metadata value comes back from the store as one.
    private static string ReadString(Interaction interaction, string key)
        => interaction.TechnicalMetadata is not null &&
            interaction.TechnicalMetadata.TryGetValue(key, out var value) &&
            value?.ToString() is { } text &&
            !string.IsNullOrWhiteSpace(text)
                ? text
                : null;
}

/// <summary>
/// Who a voicemail is for: one agent, a queue's shared voicemail box, or nobody.
/// </summary>
/// <param name="AgentId">The agent-profile identifier of the recipient agent, when the message is for one.</param>
/// <param name="SharedQueueId">The queue whose shared box receives the message, when it goes to one.</param>
public sealed record VoicemailRecipient(string AgentId, string SharedQueueId)
{
    /// <summary>
    /// Gets the recipient of a message that has nowhere to go.
    /// </summary>
    public static VoicemailRecipient None { get; } = new(null, null);

    /// <summary>
    /// Creates the recipient of a message for one agent.
    /// </summary>
    /// <param name="agentId">The agent-profile identifier.</param>
    /// <returns>The recipient.</returns>
    public static VoicemailRecipient ForAgent(string agentId) => new(agentId, null);

    /// <summary>
    /// Creates the recipient of a message for a queue's shared voicemail box.
    /// </summary>
    /// <param name="queueId">The queue identifier.</param>
    /// <returns>The recipient.</returns>
    public static VoicemailRecipient ForSharedQueue(string queueId) => new(null, queueId);
}
