namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// A pending campaign record to classify, as the reports and the dispatch diagnostics read it from the indexes.
/// </summary>
/// <param name="ActivityId">The activity identifier.</param>
/// <param name="Queued">Whether the record has an item in its campaign's queue.</param>
/// <param name="StampedProfileId">The dialer profile stamped on that queue item, if any.</param>
/// <param name="ActivityProfileId">The dialer profile the activity itself carries, if any.</param>
internal readonly record struct DialerRecordInput(
    string ActivityId,
    bool Queued,
    string StampedProfileId,
    string ActivityProfileId);
