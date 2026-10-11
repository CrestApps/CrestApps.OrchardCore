using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Taxation.Fakes;

/// <summary>
/// A deterministic, mutable <see cref="IClock"/> for taxation tests.
/// </summary>
public sealed class TestClock : IClock
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestClock"/> class.
    /// </summary>
    /// <param name="utcNow">The initial UTC value the clock returns.</param>
    public TestClock(DateTime utcNow)
    {
        UtcNow = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
    }

    /// <inheritdoc />
    public DateTime UtcNow { get; set; }

    /// <inheritdoc />
    public ITimeZone GetTimeZone(string timeZoneId) => throw new NotSupportedException();

    /// <inheritdoc />
    public ITimeZone[] GetTimeZones() => throw new NotSupportedException();

    /// <inheritdoc />
    public ITimeZone GetSystemTimeZone() => throw new NotSupportedException();

    /// <inheritdoc />
    public DateTimeOffset ConvertToTimeZone(DateTimeOffset dateTimeOffset, ITimeZone timeZone) => throw new NotSupportedException();
}
