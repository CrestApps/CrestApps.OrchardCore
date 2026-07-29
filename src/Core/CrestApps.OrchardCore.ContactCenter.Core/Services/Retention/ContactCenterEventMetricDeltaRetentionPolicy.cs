using System.Linq.Expressions;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services.Retention;

/// <summary>
/// Purges appended event count contributions. The roller normally removes each contribution as it folds it,
/// so this policy only ever finds one the roller could not fold — which is why it is aged from when the
/// contribution was appended rather than from the day it counts toward, and why it shares the daily totals'
/// window: a contribution that outlives the total it belongs to would be added to a total that is no longer
/// there. It is an aggregate rather than a record of any one interaction, so the legal-hold floor does not
/// apply.
/// </summary>
public sealed class ContactCenterEventMetricDeltaRetentionPolicy : ContactCenterRetentionPolicyBase<ContactCenterEventMetricDelta, ContactCenterEventMetricDeltaIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterEventMetricDeltaRetentionPolicy"/> class.
    /// </summary>
    /// <param name="session">The tenant YesSql session used to find expired records.</param>
    /// <param name="deltaStore">The event metric contribution store.</param>
    public ContactCenterEventMetricDeltaRetentionPolicy(
        ISession session,
        IContactCenterMetricDeltaStore deltaStore)
        : base(session, deltaStore)
    {
    }

    /// <inheritdoc/>
    public override string EntityName => "ContactCenterEventMetricDelta";

    /// <inheritdoc/>
    protected override double GetRetentionDays(ContactCenterRetentionOptions options) => options.EventMetricRetentionDays;

    /// <inheritdoc/>
    protected override Expression<Func<ContactCenterEventMetricDeltaIndex, bool>> BuildExpiredPredicate(DateTime cutoffUtc)
        => index => index.CreatedUtc < cutoffUtc;
}
