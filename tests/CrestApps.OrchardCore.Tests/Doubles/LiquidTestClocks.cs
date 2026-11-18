using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Doubles;

/// <summary>
/// An <see cref="ILocalClock"/> whose local time zone is a given one, for templates that show times to a reader.
/// </summary>
internal static class LiquidTestClocks
{
    /// <summary>
    /// Creates a local clock in a time zone.
    /// </summary>
    /// <param name="timeZoneId">The IANA or Windows id of the time zone.</param>
    /// <returns>The clock.</returns>
    public static ILocalClock In(string timeZoneId)
    {
        var timeZone = new Mock<ITimeZone>();
        timeZone.SetupGet(value => value.TimeZoneId).Returns(timeZoneId);

        var clock = new Mock<ILocalClock>();
        clock.Setup(value => value.GetLocalTimeZoneAsync()).ReturnsAsync(timeZone.Object);

        return clock.Object;
    }
}
