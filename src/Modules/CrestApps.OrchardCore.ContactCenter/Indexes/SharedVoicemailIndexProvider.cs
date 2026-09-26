using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.ContactCenter.Indexes;

/// <summary>
/// Maps <see cref="SharedVoicemail"/> documents to the <see cref="SharedVoicemailIndex"/>.
/// </summary>
public sealed class SharedVoicemailIndexProvider : IndexProvider<SharedVoicemail>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SharedVoicemailIndexProvider"/> class.
    /// </summary>
    public SharedVoicemailIndexProvider()
    {
        CollectionName = ContactCenterStorage.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<SharedVoicemail> context)
    {
        context
            .For<SharedVoicemailIndex>()
            .Map(voicemail => new SharedVoicemailIndex
            {
                ItemId = voicemail.ItemId,
                InteractionId = voicemail.InteractionId,
                QueueId = voicemail.QueueId,
                Status = voicemail.Status,
                ClaimedByUserId = voicemail.ClaimedByUserId,
                ReceivedUtc = voicemail.ReceivedUtc,
                ResolvedUtc = voicemail.ResolvedUtc,
            });
    }
}
