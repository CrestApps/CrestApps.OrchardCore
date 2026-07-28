using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.ContactCenter.Indexes;

/// <summary>
/// Maps <see cref="ContactCenterWorkState"/> documents to the <see cref="ContactCenterWorkStateIndex"/>.
/// </summary>
public sealed class ContactCenterWorkStateIndexProvider : IndexProvider<ContactCenterWorkState>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterWorkStateIndexProvider"/> class.
    /// </summary>
    public ContactCenterWorkStateIndexProvider()
    {
        CollectionName = ContactCenterConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<ContactCenterWorkState> context)
    {
        context
            .For<ContactCenterWorkStateIndex>()
            .Map(workState => new ContactCenterWorkStateIndex
            {
                ItemId = workState.ItemId,
                ActivityItemId = workState.ActivityItemId,
                AssignmentStatus = workState.AssignmentStatus,
                ReservationId = workState.ReservationId,
                ReservedById = workState.ReservedById,
                AssignedToId = workState.AssignedToId,
                ModifiedUtc = workState.ModifiedUtc,
            });
    }
}
