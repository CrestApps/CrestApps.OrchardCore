using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides a YesSql-based implementation of <see cref="IBusinessHoursCalendarStore"/>.
/// </summary>
public sealed class BusinessHoursCalendarStore : DocumentCatalog<BusinessHoursCalendar, BusinessHoursCalendarIndex>, IBusinessHoursCalendarStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessHoursCalendarStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public BusinessHoursCalendarStore(ISession session)
        : base(session)
    {
        CollectionName = ContactCenterConstants.CollectionName;
    }

    /// <inheritdoc/>
    public async Task<BusinessHoursCalendar> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        return await Session.Query<BusinessHoursCalendar, BusinessHoursCalendarIndex>(
            index => index.Name == name,
            collection: ContactCenterConstants.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<BusinessHoursCalendar>> ListEnabledAsync(CancellationToken cancellationToken = default)
    {
        var calendars = await Session.Query<BusinessHoursCalendar, BusinessHoursCalendarIndex>(
            index => index.Enabled,
            collection: ContactCenterConstants.CollectionName)
            .ListAsync(cancellationToken);

        return calendars.ToArray();
    }
}
