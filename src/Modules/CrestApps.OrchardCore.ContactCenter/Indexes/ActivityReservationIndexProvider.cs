using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.ContactCenter.Indexes;

/// <summary>
/// Maps <see cref="ActivityReservation"/> documents to the <see cref="ActivityReservationIndex"/>.
/// </summary>
public sealed class ActivityReservationIndexProvider : IndexProvider<ActivityReservation>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityReservationIndexProvider"/> class.
    /// </summary>
    public ActivityReservationIndexProvider()
    {
        CollectionName = ContactCenterConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<ActivityReservation> context)
    {
        context
            .For<ActivityReservationIndex>()
            .Map(reservation => new ActivityReservationIndex
            {
                ItemId = reservation.ItemId,
                ActivityItemId = reservation.ActivityItemId,
                AgentId = reservation.AgentId,
                Status = reservation.Status,
                ExpiresUtc = reservation.ExpiresUtc,
            });
    }
}
