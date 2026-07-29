using System.Globalization;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Formats and parses the day key that identifies a daily event metric bucket.
/// </summary>
public static class ContactCenterMetricDateKey
{
    /// <summary>
    /// The format the day key is written in.
    /// </summary>
    public const string Format = "yyyy-MM-dd";

    /// <summary>
    /// Formats the supplied instant's UTC date as a day key.
    /// </summary>
    /// <param name="occurredUtc">The instant to take the date of.</param>
    /// <returns>The day key.</returns>
    public static string From(DateTime occurredUtc)
    {
        return occurredUtc.Date.ToString(Format, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Parses a day key back into the UTC midnight it identifies.
    /// </summary>
    /// <param name="dateKey">The day key to parse.</param>
    /// <returns>The UTC date the key identifies.</returns>
    /// <exception cref="FormatException">The key is not a valid day key.</exception>
    public static DateTime Parse(string dateKey)
    {
        if (!DateTime.TryParseExact(dateKey, Format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            throw new FormatException($"The value '{dateKey}' is not a valid '{Format}' day key.");
        }

        return DateTime.SpecifyKind(date, DateTimeKind.Utc);
    }
}
