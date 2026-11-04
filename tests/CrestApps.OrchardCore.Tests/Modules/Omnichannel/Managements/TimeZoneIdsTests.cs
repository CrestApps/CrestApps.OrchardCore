using CrestApps.OrchardCore.Omnichannel.Managements.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements;

/// <summary>
/// Four Omnichannel Management files each had a private copy of the time-zone id normalization; they share this one.
/// </summary>
public sealed class TimeZoneIdsTests
{
    [Theory]
    [InlineData(" America/New_York ", "America/New_York")]
    [InlineData("Europe/London", "Europe/London")]
    [InlineData("Not/AZone", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Normalize_ReturnsTheIanaIdOrNull(string timeZoneId, string expected)
        => Assert.Equal(expected, TimeZoneIds.Normalize(timeZoneId));
}
