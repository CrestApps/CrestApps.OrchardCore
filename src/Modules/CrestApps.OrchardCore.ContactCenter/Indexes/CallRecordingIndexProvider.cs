using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.ContactCenter.Indexes;

/// <summary>
/// Maps a <see cref="CallRecording"/> to the <see cref="CallRecordingIndex"/> the call recordings page searches.
/// </summary>
public sealed class CallRecordingIndexProvider : IndexProvider<CallRecording>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CallRecordingIndexProvider"/> class.
    /// </summary>
    public CallRecordingIndexProvider()
    {
        CollectionName = ContactCenterStorage.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<CallRecording> context)
    {
        context
            .For<CallRecordingIndex>()
            .Map(recording => new CallRecordingIndex
            {
                ItemId = recording.ItemId,
                Source = recording.Source,
                ProviderRecordingId = recording.ProviderRecordingId,
                ProviderCallId = recording.ProviderCallId,
                InteractionId = recording.InteractionId,
                ActivityItemId = recording.ActivityItemId,
                AgentUserId = recording.AgentUserId,
                CustomerAddress = recording.CustomerAddress,
                Direction = recording.Direction,
                StartedUtc = recording.StartedUtc,
                DurationSeconds = recording.DurationSeconds,
                IsStored = recording.StoredUtc.HasValue,
                IsErased = recording.ErasedUtc.HasValue,
            });
    }
}
