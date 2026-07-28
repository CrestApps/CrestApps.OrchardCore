using System.Linq.Expressions;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services.Retention;

/// <summary>
/// Purges reservations that have reached a terminal state, measured from creation. The expiry cannot serve
/// as the age because a reservation accepted well before it would have expired keeps an expiry in the
/// future, which would make it look permanently young.
/// </summary>
public sealed class ActivityReservationRetentionPolicy : ContactCenterRetentionPolicyBase<ActivityReservation, ActivityReservationIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityReservationRetentionPolicy"/> class.
    /// </summary>
    /// <param name="session">The tenant YesSql session used to find expired records.</param>
    /// <param name="reservationStore">The activity reservation store.</param>
    public ActivityReservationRetentionPolicy(
        ISession session,
        IActivityReservationStore reservationStore)
        : base(session, reservationStore)
    {
    }

    /// <inheritdoc/>
    public override string EntityName => "ActivityReservation";

    /// <inheritdoc/>
    protected override double GetRetentionDays(ContactCenterRetentionOptions options) => options.ActivityReservationRetentionDays;

    /// <inheritdoc/>
    protected override Expression<Func<ActivityReservationIndex, bool>> BuildExpiredPredicate(DateTime cutoffUtc)
        => index => index.ModifiedUtc != null
            && index.ModifiedUtc < cutoffUtc
            && (index.Status == ReservationStatus.Rejected
                || index.Status == ReservationStatus.Expired
                || index.Status == ReservationStatus.Canceled);
}
