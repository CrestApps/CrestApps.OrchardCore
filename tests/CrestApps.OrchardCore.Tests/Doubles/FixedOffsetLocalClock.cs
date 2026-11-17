using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Doubles;

/// <summary>
/// A site clock at a fixed offset from UTC, such as a United States zone, so a test sees whether a date was taken in
/// the site's time zone or in UTC.
/// </summary>
internal sealed class FixedOffsetLocalClock : ILocalClock
{
    private readonly Func<DateTime> _utcNow;

    public FixedOffsetLocalClock(Func<DateTime> utcNow, TimeSpan offset)
    {
        _utcNow = utcNow;
        Offset = offset;
    }

    /// <summary>
    /// Gets or sets the site's offset from UTC.
    /// </summary>
    public TimeSpan Offset { get; set; }

    public Task<DateTimeOffset> GetLocalNowAsync()
        => Task.FromResult(new DateTimeOffset(DateTime.SpecifyKind(_utcNow(), DateTimeKind.Utc)).ToOffset(Offset));

    public Task<ITimeZone> GetLocalTimeZoneAsync()
        => throw new NotSupportedException();

    public Task<DateTimeOffset> ConvertToLocalAsync(DateTimeOffset utcDateTime)
        => Task.FromResult(utcDateTime.ToOffset(Offset));

    public Task<DateTime> ConvertToUtcAsync(DateTime dateTimeLocal)
        => Task.FromResult(DateTime.SpecifyKind(dateTimeLocal - Offset, DateTimeKind.Utc));
}
