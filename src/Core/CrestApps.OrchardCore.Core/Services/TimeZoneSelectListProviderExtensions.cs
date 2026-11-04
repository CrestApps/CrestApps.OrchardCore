using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Core.Services;

/// <summary>
/// Builds time zone pickers from <see cref="ITimeZoneSelectListProvider"/>, so every editor shows the same zones, with
/// the friendly names the Time Zones feature maps when it is on.
/// </summary>
public static class TimeZoneSelectListProviderExtensions
{
    /// <summary>
    /// Builds the options of a time zone picker, with the selected zone marked. A selected zone the provider doesn't list,
    /// such as one saved before the list was narrowed, is listed first so it isn't lost on save.
    /// </summary>
    /// <param name="provider">The time zone list provider.</param>
    /// <param name="selectedTimeZoneId">The selected time zone id, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The options.</returns>
    public static async Task<List<SelectListItem>> GetTimeZoneOptionsAsync(this ITimeZoneSelectListProvider provider, string selectedTimeZoneId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var selected = string.IsNullOrWhiteSpace(selectedTimeZoneId) ? null : selectedTimeZoneId.Trim();
        var options = (await provider.GetTimeZoneSelectListAsync(cancellationToken))
            .Select(zone => new SelectListItem(zone.Value, zone.Key, selected is not null && string.Equals(zone.Key, selected, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (selected is not null && !options.Any(option => option.Selected))
        {
            options.Insert(0, new SelectListItem(selected, selected, true));
        }

        return options;
    }
}
