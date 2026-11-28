using CrestApps.OrchardCore.AI.Core;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.AI;

/// <summary>
/// <see cref="AILocalTimeArguments.ReadPastAsync"/> reads a time entered in the site's time zone, as the admin forms take
/// it, converts it to UTC, refuses a time in the future or text that is not a time, and treats a blank as not given.
/// </summary>
public sealed class AILocalTimeArgumentsTests
{
    private static readonly DateTime _now = new(2026, 11, 20, 17, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("2026-11-12T09:30", 2026, 11, 12, 14, 30)]
    [InlineData("2026-11-12 09:30", 2026, 11, 12, 14, 30)]
    [InlineData("2026-11-12T09:30:00", 2026, 11, 12, 14, 30)]
    [InlineData("2026-11-12", 2026, 11, 12, 5, 0)]
    [InlineData("2026-07-01T09:30", 2026, 7, 1, 13, 30)]
    public async Task ReadPast_ConvertsATimeInTheSitesTimeZone_ToUtc(string text, int year, int month, int day, int hour, int minute)
    {
        // Act
        var (utc, error) = await AILocalTimeArguments.ReadPastAsync(NewYork(), Clock(), text, "Nothing was recorded", "receivedAt", "the time the payment was received");

        // Assert
        Assert.Null(error);
        Assert.Equal(new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc), utc);
        Assert.Equal(DateTimeKind.Utc, utc.Value.Kind);
    }

    [Fact]
    public async Task ReadPast_RefusesATimeInTheFuture_AndTextThatIsNotATime()
    {
        // Act
        var (_, future) = await AILocalTimeArguments.ReadPastAsync(NewYork(), Clock(), "2026-11-20T12:30", "Nothing was recorded", "receivedAt", "the time the payment was received");
        var (_, notATime) = await AILocalTimeArguments.ReadPastAsync(NewYork(), Clock(), "last Tuesday", "Nothing was recorded", "receivedAt", "the time the payment was received");

        // Assert
        Assert.Equal("Nothing was recorded: the time the payment was received can't be in the future.", future);
        Assert.Equal("Nothing was recorded: give receivedAt as yyyy-MM-ddTHH:mm in the site's time zone.", notATime);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public async Task ReadPast_TreatsABlankAsNotGiven(string text)
    {
        // Act
        var (utc, error) = await AILocalTimeArguments.ReadPastAsync(NewYork(), Clock(), text, "Nothing was recorded", "receivedAt", "the time the payment was received");

        // Assert
        Assert.Null(utc);
        Assert.Null(error);
    }

    private static ILocalClock NewYork()
    {
        var localClock = new Mock<ILocalClock>();
        localClock.Setup(clock => clock.GetLocalTimeZoneAsync()).ReturnsAsync(Mock.Of<ITimeZone>(zone => zone.TimeZoneId == "America/New_York"));

        return localClock.Object;
    }

    private static IClock Clock()
        => Mock.Of<IClock>(clock => clock.UtcNow == _now);
}
