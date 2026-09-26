using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Writes the transfer history the transfer reports read: one entry per transfer, opened when the agent asks for it
/// and completed when the caller actually reaches whoever it was sent to.
/// </summary>
/// <remarks>
/// The reports count an entry with a completion time as a completed transfer and measure the time between the two
/// stamps. Stamping both at the request made every transfer look completed and instant, including the ones where the
/// caller hung up before anyone answered.
/// </remarks>
public static class InteractionTransferHistory
{
    /// <summary>The result recorded while a transferred caller is being offered to an agent.</summary>
    public const string OfferedToAgent = "Offered to agent";

    /// <summary>The result recorded while a transferred caller waits in the destination queue.</summary>
    public const string WaitingInQueue = "Waiting in queue";

    /// <summary>The result recorded when the transferred caller was answered.</summary>
    public const string Answered = "Answered";

    /// <summary>The result recorded when the caller left the contact center for an external number.</summary>
    public const string SentToExternalNumber = "Sent to external number";

    /// <summary>The result recorded when a warm transfer was handed over after the consult.</summary>
    public const string HandedOverAfterConsult = "Handed over after consult";

    /// <summary>The result recorded when a consult ended without the transfer.</summary>
    public const string ConsultCancelled = "Consult cancelled";

    /// <summary>The result recorded while the agent consults the destination of a warm transfer.</summary>
    public const string Consulting = "Consulting";

    /// <summary>The result recorded when the caller hung up before the transfer completed.</summary>
    public const string CallerHungUp = "Caller hung up";

    /// <summary>
    /// Opens an entry for a transfer that has just been requested.
    /// </summary>
    /// <param name="interaction">The interaction being transferred.</param>
    /// <param name="fromAgentId">The agent the call is leaving.</param>
    /// <param name="targetType">The kind of destination.</param>
    /// <param name="targetId">The destination: an agent, a queue or a number.</param>
    /// <param name="requestedUtc">When the transfer was requested.</param>
    /// <param name="result">What has happened to the call so far.</param>
    /// <returns>The opened entry.</returns>
    public static InteractionTransferHistoryEntry Open(
        Interaction interaction,
        string fromAgentId,
        InteractionTransferTargetType targetType,
        string targetId,
        DateTime requestedUtc,
        string result)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        var entry = new InteractionTransferHistoryEntry
        {
            FromParticipantId = fromAgentId,
            ToParticipantId = targetId,
            TargetType = targetType.ToString(),
            RequestedUtc = requestedUtc,
            Result = result,
        };

        interaction.TransferHistory.Add(entry);

        return entry;
    }

    /// <summary>
    /// Completes the most recent entry that is still waiting for the caller to arrive.
    /// </summary>
    /// <param name="interaction">The interaction.</param>
    /// <param name="completedUtc">When the caller arrived.</param>
    /// <param name="result">The outcome.</param>
    /// <param name="toParticipantId">Who the caller reached, when that is now known more precisely than the request said.</param>
    /// <returns><see langword="true"/> when an open entry was completed.</returns>
    public static bool CompletePending(Interaction interaction, DateTime completedUtc, string result, string toParticipantId = null)
    {
        var entry = FindPending(interaction);

        if (entry is null)
        {
            return false;
        }

        entry.CompletedUtc = completedUtc;
        entry.Result = result;

        if (!string.IsNullOrEmpty(toParticipantId))
        {
            entry.ToParticipantId = toParticipantId;
        }

        return true;
    }

    /// <summary>
    /// Records why the most recent open entry will never complete, leaving it without a completion time so the
    /// reports count it as a transfer that did not land.
    /// </summary>
    /// <param name="interaction">The interaction.</param>
    /// <param name="result">Why it did not complete.</param>
    /// <returns><see langword="true"/> when an open entry was closed.</returns>
    public static bool AbandonPending(Interaction interaction, string result)
    {
        var entry = FindPending(interaction);

        if (entry is null)
        {
            return false;
        }

        entry.Result = result;

        return true;
    }

    /// <summary>
    /// Returns the most recent entry still waiting for the caller to arrive, if any.
    /// </summary>
    /// <param name="interaction">The interaction.</param>
    /// <returns>The pending entry, or <see langword="null"/>.</returns>
    public static InteractionTransferHistoryEntry FindPending(Interaction interaction)
    {
        if (interaction?.TransferHistory is null)
        {
            return null;
        }

        for (var index = interaction.TransferHistory.Count - 1; index >= 0; index--)
        {
            var entry = interaction.TransferHistory[index];

            if (entry is not null && !entry.CompletedUtc.HasValue &&
                entry.Result is OfferedToAgent or WaitingInQueue or Consulting)
            {
                return entry;
            }
        }

        return null;
    }
}
