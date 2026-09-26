using System.Linq.Expressions;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services.Retention;

/// <summary>
/// Purges daily event metric rollups. They are aggregates rather than records of any one interaction, so
/// they are not held by the legal-hold floor.
/// </summary>
public sealed class ContactCenterEventMetricRetentionPolicy : ContactCenterRetentionPolicyBase<ContactCenterEventMetric, ContactCenterEventMetricIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterEventMetricRetentionPolicy"/> class.
    /// </summary>
    /// <param name="session">The tenant YesSql session used to find expired records.</param>
    /// <param name="metricStore">The event metric store.</param>
    public ContactCenterEventMetricRetentionPolicy(
        ISession session,
        IContactCenterMetricStore metricStore)
        : base(session, metricStore)
    {
    }

    /// <inheritdoc/>
    public override string EntityName => "ContactCenterEventMetric";

    /// <inheritdoc/>
    protected override double GetRetentionDays(ContactCenterRetentionOptions options) => options.EventMetricRetentionDays;

    /// <inheritdoc/>
    protected override Expression<Func<ContactCenterEventMetricIndex, bool>> BuildExpiredPredicate(DateTime cutoffUtc)
        => index => index.Date < cutoffUtc;
}
