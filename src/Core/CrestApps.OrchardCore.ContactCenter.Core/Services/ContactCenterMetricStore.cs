using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides a YesSql-based implementation of <see cref="IContactCenterMetricStore"/>.
/// </summary>
public sealed class ContactCenterMetricStore : DocumentCatalog<ContactCenterEventMetric, ContactCenterEventMetricIndex>, IContactCenterMetricStore
{
    protected override bool CheckConcurrency => true;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterMetricStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public ContactCenterMetricStore(ISession session)
        : base(session)
    {
        CollectionName = ContactCenterConstants.CollectionName;
    }

    /// <inheritdoc/>
    public async Task<ContactCenterEventMetric> FindAsync(string dateKey, string eventType, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(dateKey);
        ArgumentException.ThrowIfNullOrEmpty(eventType);

        return await Session.Query<ContactCenterEventMetric, ContactCenterEventMetricIndex>(
            index => index.DateKey == dateKey && index.EventType == eventType,
            collection: ContactCenterConstants.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<ContactCenterEventMetric>> ListByDateRangeAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var metrics = await Session.Query<ContactCenterEventMetric, ContactCenterEventMetricIndex>(
            index => index.Date >= fromUtc && index.Date <= toUtc,
            collection: ContactCenterConstants.CollectionName)
            .ListAsync(cancellationToken);

        return metrics.ToArray();
    }
}
