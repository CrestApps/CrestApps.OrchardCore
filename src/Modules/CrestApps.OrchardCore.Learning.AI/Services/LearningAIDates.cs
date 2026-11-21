using System.Globalization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.AI.Services;

/// <summary>
/// Writes the times the Learning AI tools return in the person's time zone, never in UTC.
/// </summary>
internal static class LearningAIDates
{
    /// <summary>
    /// Writes a stored UTC time as a local date and time.
    /// </summary>
    /// <param name="localClock">The local clock.</param>
    /// <param name="utc">The stored time, in UTC.</param>
    /// <returns>The local time as <c>yyyy-MM-dd HH:mm</c>, or <see langword="null"/>.</returns>
    public static async Task<string> FormatAsync(ILocalClock localClock, DateTime? utc)
    {
        if (utc is not { } value)
        {
            return null;
        }

        var local = await localClock.ConvertToLocalAsync(new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)));

        return local.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Gets the id of the time zone the times are written in.
    /// </summary>
    /// <param name="localClock">The local clock.</param>
    /// <returns>The time zone id.</returns>
    public static async Task<string> TimeZoneAsync(ILocalClock localClock)
        => (await localClock.GetLocalTimeZoneAsync())?.TimeZoneId;
}
