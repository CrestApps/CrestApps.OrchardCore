using CrestApps.OrchardCore.Scheduling.Core.Indexes;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Scheduling.Core.Services;

/// <summary>
/// Stores appointment types.
/// </summary>
public sealed class AppointmentTypeStore : DocumentCatalog<AppointmentType, AppointmentTypeIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AppointmentTypeStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public AppointmentTypeStore(ISession session)
        : base(session)
    {
        CollectionName = SchedulingConstants.CollectionName;
    }

    /// <summary>
    /// Lists every appointment type, by name.
    /// </summary>
    /// <returns>The types.</returns>
    public Task<IReadOnlyList<AppointmentType>> ListAsync()
    {
        return ListByNameAsync(index => index.Name);
    }

    /// <summary>
    /// Finds an appointment type by the address of its booking page.
    /// </summary>
    /// <param name="slug">The address.</param>
    /// <returns>The type, or <see langword="null"/>.</returns>
    public async Task<AppointmentType> FindBySlugAsync(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return null;
        }

        return await LoadedAsync(await Session.Query<AppointmentType, AppointmentTypeIndex>(index => index.Slug == slug, collection: CollectionName).FirstOrDefaultAsync());
    }
}
