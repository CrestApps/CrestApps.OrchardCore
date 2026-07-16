using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.ContactCenter.Indexes;

/// <summary>
/// Maps <see cref="ContactCenterProjectionCheckpoint"/> documents to the <see cref="ContactCenterProjectionCheckpointIndex"/>.
/// </summary>
public sealed class ContactCenterProjectionCheckpointIndexProvider : IndexProvider<ContactCenterProjectionCheckpoint>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterProjectionCheckpointIndexProvider"/> class.
    /// </summary>
    public ContactCenterProjectionCheckpointIndexProvider()
    {
        CollectionName = ContactCenterConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<ContactCenterProjectionCheckpoint> context)
    {
        context
            .For<ContactCenterProjectionCheckpointIndex>()
            .Map(checkpoint => new ContactCenterProjectionCheckpointIndex
            {
                ItemId = checkpoint.ItemId,
                HandlerId = checkpoint.HandlerId,
                Version = checkpoint.Version,
            });
    }
}
