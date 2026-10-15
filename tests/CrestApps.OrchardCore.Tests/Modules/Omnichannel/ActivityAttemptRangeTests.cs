using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel;

public sealed class ActivityAttemptRangeTests
{
    [Theory]
    [InlineData("4", 4, 4, "4")]
    [InlineData(" 4 ", 4, 4, "4")]
    [InlineData("4+", 4, null, "4+")]
    [InlineData("4-", null, 4, "4-")]
    [InlineData("2..5", 2, 5, "2..5")]
    [InlineData("4..", 4, null, "4+")]
    [InlineData("..4", null, 4, "4-")]
    [InlineData("0", 0, 0, "0")]
    public void TryParse_ReadsWhatAnOperatorTypes(string value, int? minimum, int? maximum, string written)
    {
        // Act
        var parsed = ActivityAttemptRange.TryParse(value, out var range);

        // Assert
        Assert.True(parsed);
        Assert.Equal(minimum, range.Minimum);
        Assert.Equal(maximum, range.Maximum);
        Assert.Equal(written, range.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("four")]
    [InlineData("..")]
    [InlineData("5..2")]
    [InlineData("-4")]
    [InlineData("4++")]
    [InlineData("2..x")]
    public void TryParse_RefusesWhatIsNotARange(string value)
    {
        // Act and assert
        Assert.False(ActivityAttemptRange.TryParse(value, out _));
    }
}
