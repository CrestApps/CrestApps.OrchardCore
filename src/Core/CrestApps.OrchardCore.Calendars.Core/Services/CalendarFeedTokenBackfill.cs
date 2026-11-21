using CrestApps.OrchardCore.Calendars.Core.Indexes;
using CrestApps.OrchardCore.Calendars.Models;
using Microsoft.AspNetCore.DataProtection;
using YesSql;

namespace CrestApps.OrchardCore.Calendars.Core.Services;

/// <summary>
/// Gives every calendar whose feed secret was stored before secrets were hashed its hash and its protected copy, and
/// saves it so the index holds the hash. The plain secret is kept for one release, so the previous release still opens
/// the feed if it is rolled back.
/// </summary>
public sealed class CalendarFeedTokenBackfill
{
    private readonly ISession _session;
    private readonly IDataProtectionProvider _dataProtectionProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarFeedTokenBackfill"/> class.
    /// </summary>
    public CalendarFeedTokenBackfill(ISession session, IDataProtectionProvider dataProtectionProvider)
    {
        _session = session;
        _dataProtectionProvider = dataProtectionProvider;
    }

    /// <summary>
    /// Runs the backfill.
    /// </summary>
    /// <returns>How many calendars changed.</returns>
    public async Task<int> RunAsync()
    {
        var changed = 0;
        var calendars = await _session.Query<Calendar, CalendarIndex>(index => index.FeedToken != null, collection: CalendarsConstants.CollectionName).ListAsync();

        foreach (var calendar in calendars)
        {
            if (CalendarFeedTokens.Backfill(calendar, _dataProtectionProvider))
            {
                await _session.SaveAsync(calendar, false, CalendarsConstants.CollectionName);
                changed++;
            }
        }

        await _session.SaveChangesAsync();

        return changed;
    }
}
