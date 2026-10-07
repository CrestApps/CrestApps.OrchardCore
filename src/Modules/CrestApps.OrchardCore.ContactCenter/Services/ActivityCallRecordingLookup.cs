using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Finds the playable recordings of the calls a CRM activity is about.
/// </summary>
/// <remarks>
/// A recording names the activity when the call that made it knew it, as an AI voice agent's call does. A routed or
/// dialed agent call is a Contact Center interaction, and its recording may name only the interaction, which in turn
/// names the activity. Both are looked up, and a recording found both ways is listed once.
/// </remarks>
internal sealed class ActivityCallRecordingLookup
{
    // An activity is one conversation; more recordings than this on it would be a defect, not a list to page through.
    private const int MaxRecordings = 100;

    private readonly ICallRecordingStore _store;
    private readonly IInteractionManager _interactionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityCallRecordingLookup"/> class.
    /// </summary>
    /// <param name="store">The call recordings catalog.</param>
    /// <param name="interactionManager">The interaction manager, read for the interaction of the activity's call.</param>
    public ActivityCallRecordingLookup(ICallRecordingStore store, IInteractionManager interactionManager)
    {
        _store = store;
        _interactionManager = interactionManager;
    }

    /// <summary>
    /// Lists the playable recordings of an activity's calls: stored and not erased.
    /// </summary>
    /// <param name="activityItemId">The activity identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The recordings, oldest first; empty when there are none.</returns>
    public async Task<IReadOnlyList<CallRecording>> ListPlayableAsync(string activityItemId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(activityItemId))
        {
            return [];
        }

        var page = await _store.QueryAsync(new CallRecordingQuery
        {
            ActivityItemId = activityItemId,
            PageSize = MaxRecordings,
        }, cancellationToken);

        var recordings = new List<CallRecording>(page.Entries);
        var interaction = await _interactionManager.FindByActivityIdAsync(activityItemId, cancellationToken);

        if (!string.IsNullOrEmpty(interaction?.ItemId))
        {
            recordings.AddRange(await _store.ListByInteractionIdAsync(interaction.ItemId, cancellationToken));
        }

        return recordings
            .Where(CallRecordingAccess.IsPlayable)
            .DistinctBy(recording => recording.ItemId, StringComparer.Ordinal)
            .OrderBy(recording => recording.StartedUtc)
            .ThenBy(recording => recording.ItemId, StringComparer.Ordinal)
            .ToArray();
    }
}
