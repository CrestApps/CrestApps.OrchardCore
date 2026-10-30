using CrestApps.OrchardCore.Scheduling.Core.Indexes;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Scheduling.Core.Services;

/// <summary>
/// Stores waitlist entries.
/// </summary>
public sealed class WaitlistStore : DocumentCatalog<WaitlistEntry, WaitlistEntryIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WaitlistStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public WaitlistStore(ISession session)
        : base(session)
    {
        CollectionName = SchedulingConstants.CollectionName;
    }

    /// <summary>
    /// Finds the oldest waiting entry of a type whose wanted times include a moment.
    /// </summary>
    /// <param name="appointmentTypeId">The appointment type.</param>
    /// <param name="startUtc">The moment.</param>
    /// <returns>The entry, or <see langword="null"/>.</returns>
    public async Task<WaitlistEntry> FindFirstWaitingAsync(string appointmentTypeId, DateTime startUtc)
    {
        return await LoadedAsync(await Session.Query<WaitlistEntry, WaitlistEntryIndex>(
                index => index.AppointmentTypeId == appointmentTypeId && index.Status == WaitlistStatus.Waiting && index.FromUtc <= startUtc && index.ToUtc > startUtc,
                collection: CollectionName)
            .OrderBy(index => index.CreatedUtc)
            .FirstOrDefaultAsync());
    }

    /// <summary>
    /// Lists a page of entries, newest first.
    /// </summary>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">How many to return.</param>
    /// <returns>How many there are, and the page.</returns>
    public async Task<(int Count, IReadOnlyList<WaitlistEntry> Entries)> PageAsync(int skip, int take)
    {
        var query = Session.Query<WaitlistEntry, WaitlistEntryIndex>(collection: CollectionName);
        var count = await query.CountAsync();

        return (count, await LoadedAsync(await query.OrderByDescending(index => index.CreatedUtc).Skip(skip).Take(take).ListAsync()));
    }
}
