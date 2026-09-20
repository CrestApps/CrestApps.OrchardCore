using CrestApps.Core.ContactCenter.Services;
using CrestApps.Core.ContactCenter;
using CrestApps.Core.Data.YesSql.Services;
using CrestApps.Core.Data.YesSql.ContactCenter.Indexes;
using CrestApps.Core.ContactCenter.Models;
using YesSql;

namespace CrestApps.Core.Data.YesSql.ContactCenter.Services;

/// <summary>
/// Provides a YesSql-based implementation of <see cref="IBusinessHoursCalendarStore"/>.
/// </summary>
public sealed class BusinessHoursCalendarStore : ConcurrentDocumentCatalog<BusinessHoursCalendar, BusinessHoursCalendarIndex>, IBusinessHoursCalendarStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessHoursCalendarStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public BusinessHoursCalendarStore(ISession session)
        : base(session)
    {
        CollectionName = ContactCenterStorage.CollectionName;
    }

    /// <inheritdoc/>
    public async Task<BusinessHoursCalendar> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        return await Session.Query<BusinessHoursCalendar, BusinessHoursCalendarIndex>(
            index => index.Name == name,
            collection: ContactCenterStorage.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<BusinessHoursCalendar>> GetEnabledAsync(CancellationToken cancellationToken = default)
    {
        var calendars = await Session.Query<BusinessHoursCalendar, BusinessHoursCalendarIndex>(
            index => index.Enabled,
            collection: ContactCenterStorage.CollectionName)
            .ListAsync(cancellationToken);

        return calendars.ToArray();
    }
}
