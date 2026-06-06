using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

internal static class OmnichannelTimeZoneHelper
{
    public static List<SelectListItem> GetTimeZoneOptions(
        IClock clock,
        LocalizedString emptyOptionText,
        string selectedTimeZoneId)
    {
        ArgumentNullException.ThrowIfNull(clock);

        var options = new List<SelectListItem>
        {
            new()
            {
                Text = emptyOptionText.Value,
                Value = string.Empty,
                Selected = string.IsNullOrEmpty(selectedTimeZoneId),
            },
        };

        foreach (var timeZone in clock.GetTimeZones().OrderBy(x => x.TimeZoneId, StringComparer.Ordinal))
        {
            options.Add(new SelectListItem
            {
                Text = timeZone.TimeZoneId,
                Value = timeZone.TimeZoneId,
                Selected = string.Equals(timeZone.TimeZoneId, selectedTimeZoneId, StringComparison.OrdinalIgnoreCase),
            });
        }

        return options;
    }

    public static string NormalizeTimeZoneId(IClock clock, string timeZoneId)
    {
        ArgumentNullException.ThrowIfNull(clock);

        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return null;
        }

        return clock.GetTimeZones()
            .FirstOrDefault(x => x.TimeZoneId.Equals(timeZoneId.Trim(), StringComparison.OrdinalIgnoreCase))
            ?.TimeZoneId;
    }
}
