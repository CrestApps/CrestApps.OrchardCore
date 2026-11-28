using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// The rules the entry point editor applies to what it is given before the entry point is validated. The entry point
/// editors and the AI entry point tool both apply them, so an entry point saved either way is stored the same way.
/// </summary>
internal static class ContactCenterEntryPointEditorRules
{
    /// <summary>
    /// Keeps only the target of the selected kind of routing. An entry point routes to a specific agent or a queue,
    /// never both: a specific-agent target rings that agent directly and keeps no fallback queue, so the queue is
    /// cleared for an agent target and the agent for a queue target; the AI profile is kept only for an AI agent target.
    /// </summary>
    /// <param name="entryPoint">The entry point, with its target type and the targets that were entered.</param>
    public static void ApplyTarget(ContactCenterEntryPoint entryPoint)
    {
        ArgumentNullException.ThrowIfNull(entryPoint);

        var isAgentTarget = entryPoint.TargetType == EntryPointTargetType.Agent;
        var isQueueTarget = entryPoint.TargetType == EntryPointTargetType.Queue;

        entryPoint.TargetAgentId = isAgentTarget && !string.IsNullOrWhiteSpace(entryPoint.TargetAgentId)
            ? entryPoint.TargetAgentId.Trim()
            : null;
        entryPoint.TargetQueueId = isQueueTarget && !string.IsNullOrWhiteSpace(entryPoint.TargetQueueId)
            ? entryPoint.TargetQueueId.Trim()
            : null;

        if (entryPoint.TargetType != EntryPointTargetType.AIAgent)
        {
            entryPoint.TargetAIProfileId = null;
        }
    }

    /// <summary>
    /// Applies the call settings' rules: the ring window is always stored as a valid positive value, and only a queue
    /// line keeps a mailbox of its own. A personal line's messages always go to its agent, and a line that delivers to
    /// the queue's shared box has no agent inbox, so the agent selection is cleared rather than kept looking as if it
    /// still received the line's messages.
    /// </summary>
    /// <param name="entryPoint">The entry point, with the call settings that were entered.</param>
    /// <param name="targetType">The kind of routing selected.</param>
    public static void ApplyVoice(ContactCenterEntryPoint entryPoint, EntryPointTargetType targetType)
    {
        ArgumentNullException.ThrowIfNull(entryPoint);

        var isAgentTarget = targetType == EntryPointTargetType.Agent;

        entryPoint.RingTimeoutSeconds = entryPoint.RingTimeoutSeconds <= 0
            ? ContactCenterConstants.DirectRouting.DefaultRingTimeoutSeconds
            : Math.Clamp(
                entryPoint.RingTimeoutSeconds,
                ContactCenterConstants.DirectRouting.MinimumRingTimeoutSeconds,
                ContactCenterConstants.DirectRouting.MaximumRingTimeoutSeconds);

        var deliversToSharedBox = !isAgentTarget && entryPoint.VoicemailDestination == EntryPointVoicemailDestination.QueueSharedBox;

        entryPoint.VoicemailDestination = deliversToSharedBox
            ? EntryPointVoicemailDestination.QueueSharedBox
            : EntryPointVoicemailDestination.AgentInbox;
        entryPoint.VoicemailRecipientAgentId = !isAgentTarget && !deliversToSharedBox && !string.IsNullOrWhiteSpace(entryPoint.VoicemailRecipientAgentId)
            ? entryPoint.VoicemailRecipientAgentId.Trim()
            : null;
    }
}
