using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class DialerActivitySourceHelperTests
{
    [Theory]
    [InlineData(DialerMode.Manual, ActivitySources.PreviewDial)]
    [InlineData(DialerMode.Preview, ActivitySources.PreviewDial)]
    [InlineData(DialerMode.Power, ActivitySources.PowerDial)]
    [InlineData(DialerMode.Progressive, ActivitySources.ProgressiveDial)]
    [InlineData(DialerMode.Predictive, ActivitySources.PredictiveDial)]
    public void GetActivitySource_WhenModeProvided_ReturnsExpectedSource(
        DialerMode mode,
        string expectedSource)
    {
        // Act
        var actualSource = DialerActivitySourceHelper.GetActivitySource(mode);

        // Assert
        Assert.Equal(expectedSource, actualSource);
    }

    [Theory]
    [InlineData(ActivitySources.Dialer, true)]
    [InlineData(ActivitySources.PreviewDial, true)]
    [InlineData(ActivitySources.PowerDial, true)]
    [InlineData(ActivitySources.ProgressiveDial, true)]
    [InlineData(ActivitySources.PredictiveDial, true)]
    [InlineData("powerdial", true)]
    [InlineData(ActivitySources.Inbound, false)]
    [InlineData(ActivitySources.Manual, false)]
    public void IsDialerSource_WhenSourceProvided_ReturnsExpectedResult(string source, bool expected)
    {
        // Act
        var result = DialerActivitySourceHelper.IsDialerSource(source);

        // Assert
        Assert.Equal(expected, result);
    }
}
