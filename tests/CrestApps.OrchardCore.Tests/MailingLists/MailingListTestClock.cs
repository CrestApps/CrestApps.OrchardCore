using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.MailingLists;

/// <summary>
/// A clock the mailing lists tests move by hand.
/// </summary>
internal sealed class MailingListTestClock : IClock
{
    public DateTime UtcNow { get; set; } = new(2026, 11, 22, 10, 0, 0, DateTimeKind.Utc);

    public void Advance(TimeSpan by)
        => UtcNow = UtcNow.Add(by);

    public ITimeZone[] GetTimeZones()
        => [];

    public ITimeZone GetTimeZone(string timeZoneId)
        => null;

    public ITimeZone GetSystemTimeZone()
        => null;

    public DateTimeOffset ConvertToTimeZone(DateTimeOffset dateTimeOffset, ITimeZone timeZone)
        => dateTimeOffset;
}
