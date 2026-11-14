using System.Globalization;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Core.Liquid;

/// <summary>
/// Builds the <see cref="RestrictedLiquidContext"/> of the current request or background job: the user's or the site's
/// time zone from Orchard's <see cref="ILocalClock"/>, the current culture, and <c>t</c> through a localizer.
/// </summary>
public static class RestrictedLiquidContexts
{
    /// <summary>
    /// Builds the context.
    /// </summary>
    /// <param name="localClock">The local clock.</param>
    /// <param name="localizer">The localizer <c>t</c> looks texts up in, if any.</param>
    /// <returns>The context.</returns>
    public static async Task<RestrictedLiquidContext> CreateAsync(ILocalClock localClock, IStringLocalizer localizer = null)
    {
        ArgumentNullException.ThrowIfNull(localClock);

        return new RestrictedLiquidContext
        {
            TimeZone = Find((await localClock.GetLocalTimeZoneAsync())?.TimeZoneId),
            Culture = CultureInfo.CurrentUICulture,
            // The texts come from the site's own templates, so they can't be extracted to translation templates.
#pragma warning disable OCLOC104
            Localize = localizer is null ? null : (text, arguments) => localizer[text, arguments].Value,
#pragma warning restore OCLOC104
        };
    }

    private static TimeZoneInfo Find(string timeZoneId)
    {
        if (string.IsNullOrEmpty(timeZoneId))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
