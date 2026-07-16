using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class DialerModeExtensionsTests
{
    [Theory]
    [InlineData(DialerMode.Manual, false)]
    [InlineData(DialerMode.Preview, false)]
    [InlineData(DialerMode.Power, true)]
    [InlineData(DialerMode.Progressive, true)]
    [InlineData(DialerMode.Predictive, true)]
    public void IsAutomated_ClassifiesPacingModes(DialerMode mode, bool expected)
    {
        // Act
        var isAutomated = mode.IsAutomated();

        // Assert
        Assert.Equal(expected, isAutomated);
    }

    [Theory]
    [InlineData(DialerMode.Manual, false)]
    [InlineData(DialerMode.Preview, false)]
    [InlineData(DialerMode.Power, true)]
    [InlineData(DialerMode.Progressive, true)]
    [InlineData(DialerMode.Predictive, false)]
    public void RequiresAutomatedDialerFeature_IdentifiesGatedModes(DialerMode mode, bool expected)
    {
        // Act
        var requiresFeature = mode.RequiresAutomatedDialerFeature();

        // Assert
        Assert.Equal(expected, requiresFeature);
    }
}
