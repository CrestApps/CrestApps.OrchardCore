using CrestApps.Core.ContactCenter.Services;
using CrestApps.Core.ContactCenter;
using CrestApps.Core.Data.YesSql.Services;
using CrestApps.Core.Data.YesSql.ContactCenter.Indexes;
using CrestApps.Core.ContactCenter.Models;
using YesSql;

namespace CrestApps.Core.Data.YesSql.ContactCenter.Services;

/// <summary>
/// Provides a YesSql-based implementation of <see cref="IContactCenterMetricStore"/>.
/// </summary>
public sealed class ContactCenterMetricStore : ConcurrentDocumentCatalog<ContactCenterEventMetric, ContactCenterEventMetricIndex>, IContactCenterMetricStore
{
    protected override bool CheckConcurrency => true;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterMetricStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public ContactCenterMetricStore(ISession session)
        : base(session)
    {
        CollectionName = ContactCenterStorage.CollectionName;
    }

    /// <inheritdoc/>
    public async Task<ContactCenterEventMetric> FindAsync(string dateKey, string eventType, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(dateKey);
        ArgumentException.ThrowIfNullOrEmpty(eventType);

        return await Session.Query<ContactCenterEventMetric, ContactCenterEventMetricIndex>(
            index => index.DateKey == dateKey && index.EventType == eventType,
            collection: ContactCenterStorage.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<ContactCenterEventMetric>> GetByDateRangeAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var metrics = await Session.Query<ContactCenterEventMetric, ContactCenterEventMetricIndex>(
            index => index.Date >= fromUtc && index.Date <= toUtc,
            collection: ContactCenterStorage.CollectionName)
            .ListAsync(cancellationToken);

        return metrics.ToArray();
    }
}
