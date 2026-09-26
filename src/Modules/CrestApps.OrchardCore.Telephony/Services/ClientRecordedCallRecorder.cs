using CrestApps.OrchardCore.Telephony.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// Records what the soft phone reports about the calls it placed itself: that they are still up, and that they ended.
/// See <see cref="ClientRecordedCallPolicy"/>.
/// </summary>
internal sealed class ClientRecordedCallRecorder
{
    /// <summary>
    /// The most calls one report may speak for. A phone holds a handful at most; anything past this is not a phone.
    /// </summary>
    internal const int MaxCallsPerReport = 10;

    private readonly ITelephonyInteractionStore _interactionStore;
    private readonly IClock _clock;

    public ClientRecordedCallRecorder(
        ITelephonyInteractionStore interactionStore,
        IClock clock)
    {
        _interactionStore = interactionStore;
        _clock = clock;
    }

    /// <summary>
    /// Records that the user's phone still has these calls up.
    /// </summary>
    /// <param name="userId">The user whose phone reported.</param>
    /// <param name="callIds">The client call identifiers of the calls still up.</param>
    /// <param name="connectedCallIds">Those of them that have connected.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>How many calls the report kept alive.</returns>
    public async Task<int> MarkAliveAsync(
        string userId,
        IReadOnlyCollection<string> callIds,
        IReadOnlyCollection<string> connectedCallIds,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId) || callIds is null || callIds.Count == 0)
        {
            return 0;
        }

        var connected = new HashSet<string>(connectedCallIds ?? [], StringComparer.Ordinal);
        var utcNow = _clock.UtcNow;
        var marked = 0;

        foreach (var callId in callIds.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).Take(MaxCallsPerReport))
        {
            var interaction = await _interactionStore.FindByCallIdAsync(userId, callId, cancellationToken);

            if (!ClientRecordedCallPolicy.IsClientRecorded(interaction) || interaction.Outcome != CallOutcome.InProgress)
            {
                continue;
            }

            var changed = false;

            await _interactionStore.UpdateByIdAsync(
                interaction.InteractionId,
                candidate => changed = ClientRecordedCallPolicy.MarkAlive(candidate, connected.Contains(callId), utcNow),
                cancellationToken);

            if (changed)
            {
                marked++;
            }
        }

        return marked;
    }

    /// <summary>
    /// Settles a call from the phone's report of its end. A call already given an outcome -- by an earlier report, or by
    /// the sweep -- is left as it is.
    /// </summary>
    /// <param name="userId">The user whose phone reported.</param>
    /// <param name="callId">The client call identifier.</param>
    /// <param name="connected">Whether the phone says the call connected before it ended.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>Whether the report settled the call.</returns>
    public async Task<bool> SettleAsync(string userId, string callId, bool connected, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(callId))
        {
            return false;
        }

        var interaction = await _interactionStore.FindByCallIdAsync(userId, callId, cancellationToken);

        if (interaction is null || interaction.Outcome != CallOutcome.InProgress)
        {
            return false;
        }

        var utcNow = _clock.UtcNow;
        var settled = false;

        await _interactionStore.UpdateByIdAsync(
            interaction.InteractionId,
            candidate => settled = ClientRecordedCallPolicy.SettleReported(candidate, connected, utcNow),
            cancellationToken);

        return settled;
    }
}
