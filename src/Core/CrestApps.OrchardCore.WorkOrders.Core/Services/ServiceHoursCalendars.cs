using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.WorkOrders.Services;

namespace CrestApps.OrchardCore.WorkOrders.Core.Services;

/// <summary>
/// Lists the hours calendars every <see cref="IServiceHoursProvider"/> knows, for the service level policy editor, so
/// WorkOrders offers them without knowing which module keeps them.
/// </summary>
public static class ServiceHoursCalendars
{
    /// <summary>
    /// Returns the calendars of every provider, each once (the first provider to list an id names it), ordered by name.
    /// </summary>
    /// <param name="providers">The service hours providers.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The calendars.</returns>
    public static async Task<IReadOnlyList<ServiceHoursCalendar>> ListAsync(IEnumerable<IServiceHoursProvider> providers, CancellationToken cancellationToken = default)
    {
        var calendars = new Dictionary<string, ServiceHoursCalendar>(StringComparer.Ordinal);

        foreach (var provider in providers ?? [])
        {
            foreach (var calendar in await provider.ListCalendarsAsync(cancellationToken) ?? [])
            {
                if (!string.IsNullOrEmpty(calendar?.Id))
                {
                    calendars.TryAdd(calendar.Id, calendar);
                }
            }
        }

        return [.. calendars.Values.OrderBy(calendar => calendar.Name, StringComparer.CurrentCultureIgnoreCase)];
    }
}
