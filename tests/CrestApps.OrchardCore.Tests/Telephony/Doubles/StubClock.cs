using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Telephony.Doubles;

/// <summary>
/// A deterministic <see cref="IClock"/> used by telephony tests.
/// </summary>
internal sealed class StubClock : IClock
{
    private readonly DateTime _utcNow;

    public StubClock()
        : this(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc))
    {
    }

    public StubClock(DateTime utcNow)
    {
        _utcNow = utcNow;
    }

    public DateTime UtcNow => _utcNow;

    public ITimeZone GetTimeZone(string timeZoneId) => throw new NotSupportedException();

    public ITimeZone[] GetTimeZones() => throw new NotSupportedException();

    public ITimeZone GetSystemTimeZone() => throw new NotSupportedException();

    public DateTimeOffset ConvertToTimeZone(DateTimeOffset dateTimeOffset, ITimeZone timeZone) => throw new NotSupportedException();
}
