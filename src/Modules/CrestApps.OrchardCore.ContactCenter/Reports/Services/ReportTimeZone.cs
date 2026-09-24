using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Services;

/// <summary>
/// The time zone a report's days and times of day are in: the tenant's, because a workday is a local day.
/// </summary>
/// <remarks>
/// The report's period is chosen as local dates and converted to UTC at the tenant's midnight, so a report that then
/// grouped by the UTC day labelled a local day as a UTC one and split every evening across two dates. Its days and
/// times are read in the same zone the period was chosen in, and the report names that zone.
/// </remarks>
internal sealed class ReportTimeZone
{
    private ReportTimeZone(TimeZoneInfo zone, string name)
    {
        Zone = zone;
        Name = name;
    }

    /// <summary>
    /// Gets the zone that treats every day as a UTC day.
    /// </summary>
    public static ReportTimeZone Utc { get; } = new(TimeZoneInfo.Utc, "UTC");

    /// <summary>
    /// Gets the zone.
    /// </summary>
    public TimeZoneInfo Zone { get; }

    /// <summary>
    /// Gets the name a report labels its days and times with, such as <c>America/Los_Angeles</c>.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Resolves the tenant's time zone.
    /// </summary>
    /// <param name="localClock">The tenant's local clock.</param>
    /// <returns>The tenant's zone, or UTC when it names none this machine knows.</returns>
    public static async Task<ReportTimeZone> ResolveAsync(ILocalClock localClock)
    {
        ArgumentNullException.ThrowIfNull(localClock);

        var timeZone = await localClock.GetLocalTimeZoneAsync();

        return FromId(timeZone?.TimeZoneId);
    }

    /// <summary>
    /// Creates the zone of an identifier.
    /// </summary>
    /// <param name="timeZoneId">An IANA or Windows time zone identifier.</param>
    /// <returns>The zone, or UTC when the identifier is empty or unknown.</returns>
    public static ReportTimeZone FromId(string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return Utc;
        }

        try
        {
            return new ReportTimeZone(TimeZoneInfo.FindSystemTimeZoneById(timeZoneId), timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return Utc;
        }
    }

    /// <summary>
    /// Converts an instant to this zone's wall-clock time.
    /// </summary>
    /// <param name="utc">The instant.</param>
    /// <returns>The local time.</returns>
    public DateTime ToLocal(DateTime utc)
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);

    /// <summary>
    /// Gets the local date an instant falls on.
    /// </summary>
    /// <param name="utc">The instant.</param>
    /// <returns>The local date.</returns>
    public DateOnly DateOf(DateTime utc)
        => DateOnly.FromDateTime(ToLocal(utc));

    /// <summary>
    /// Gets the first local midnight after an instant, as an instant.
    /// </summary>
    /// <param name="utc">The instant.</param>
    /// <returns>The next local midnight, in UTC; always later than <paramref name="utc"/>.</returns>
    /// <remarks>
    /// A zone whose clocks jump forward at midnight has no midnight that day, so the day starts at the first local
    /// time that exists after it.
    /// </remarks>
    public DateTime NextMidnightUtc(DateTime utc)
    {
        var midnight = ToLocal(utc).Date.AddDays(1);

        while (Zone.IsInvalidTime(midnight))
        {
            midnight = midnight.AddMinutes(1);
        }

        var next = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(midnight, DateTimeKind.Unspecified), Zone);

        return next > utc ? next : utc.AddDays(1);
    }
}
