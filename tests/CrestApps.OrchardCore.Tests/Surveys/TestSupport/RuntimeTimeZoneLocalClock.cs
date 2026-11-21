using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Surveys.TestSupport;

/// <summary>
/// A local clock in a real time zone, so tests cross daylight saving changes.
/// </summary>
internal sealed class RuntimeTimeZoneLocalClock : ILocalClock
{
    private readonly TimeZoneInfo _zone;
    private readonly Func<DateTime> _utcNow;

    public RuntimeTimeZoneLocalClock(string zoneId, Func<DateTime> utcNow)
    {
        _zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        _utcNow = utcNow;
    }

    public Task<DateTimeOffset> GetLocalNowAsync()
        => ConvertToLocalAsync(new DateTimeOffset(DateTime.SpecifyKind(_utcNow(), DateTimeKind.Utc)));

    public Task<ITimeZone> GetLocalTimeZoneAsync()
        => throw new NotSupportedException();

    public Task<DateTimeOffset> ConvertToLocalAsync(DateTimeOffset utcDateTime)
        => Task.FromResult(TimeZoneInfo.ConvertTime(utcDateTime, _zone));

    public Task<DateTime> ConvertToUtcAsync(DateTime dateTimeLocal)
        => Task.FromResult(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(dateTimeLocal, DateTimeKind.Unspecified), _zone));
}
