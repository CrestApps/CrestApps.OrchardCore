using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Chat.Reports;

/// <summary>
/// Clocks for report tests whose site time zone is UTC.
/// </summary>
internal static class ReportTestClocks
{
    public static IClock Clock()
    {
        var clock = new Mock<IClock>();
        clock
            .Setup(value => value.ConvertToTimeZone(It.IsAny<DateTimeOffset>(), It.IsAny<ITimeZone>()))
            .Returns((DateTimeOffset value, ITimeZone _) => value);

        return clock.Object;
    }

    public static ILocalClock LocalClock()
    {
        var localClock = new Mock<ILocalClock>();
        localClock.Setup(value => value.GetLocalTimeZoneAsync()).ReturnsAsync(Mock.Of<ITimeZone>());

        return localClock.Object;
    }

    /// <summary>
    /// A local clock whose site time zone is the given offset from UTC.
    /// </summary>
    public static ILocalClock LocalClock(TimeSpan offset)
    {
        var localClock = new Mock<ILocalClock>();
        localClock.Setup(value => value.GetLocalTimeZoneAsync()).ReturnsAsync(Mock.Of<ITimeZone>());
        localClock
            .Setup(value => value.ConvertToLocalAsync(It.IsAny<DateTimeOffset>()))
            .ReturnsAsync((DateTimeOffset value) => value.ToOffset(offset));

        return localClock.Object;
    }
}
