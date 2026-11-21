using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Surveys.TestSupport;

/// <summary>
/// A clock the test moves.
/// </summary>
internal sealed class RuntimeTestClock : IClock
{
    public DateTime UtcNow { get; set; }

    public ITimeZone[] GetTimeZones()
        => [];

    public ITimeZone GetTimeZone(string timeZoneId)
        => null;

    public ITimeZone GetSystemTimeZone()
        => null;

    public DateTimeOffset ConvertToTimeZone(DateTimeOffset dateTimeOffset, ITimeZone timeZone)
        => dateTimeOffset;
}
