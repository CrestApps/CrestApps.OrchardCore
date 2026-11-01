using CrestApps.OrchardCore.AI.WebMcp.Models;

namespace CrestApps.OrchardCore.Tests.AI.WebMcp;

public sealed class WebMcpSettingsTests
{
    [Fact]
    public void NewSettings_ExposeNothingAndOnlyTurnOnAdminPages()
    {
        // Act
        var settings = new WebMcpSettings();

        // Assert
        Assert.False(settings.ExposeAllTools);
        Assert.Empty(settings.Tools);
        Assert.True(settings.EnableOnAdminPages);
        Assert.False(settings.EnableOnFrontEndPages);
    }

    [Theory]
    [InlineData(true, false, true, true)]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, true)]
    [InlineData(false, true, true, false)]
    public void IsEnabledFor_FollowsThePageKind(bool enableAdmin, bool enableFrontEnd, bool isAdminPage, bool expected)
    {
        // Arrange
        var options = new WebMcpPageOptions
        {
            EnableOnAdminPages = enableAdmin,
            EnableOnFrontEndPages = enableFrontEnd,
        };

        // Act
        var enabled = options.IsEnabledFor(isAdminPage);

        // Assert
        Assert.Equal(expected, enabled);
    }
}
