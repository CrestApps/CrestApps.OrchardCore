using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Tests.Core.Omnichannel.Models;

public sealed class OmnichannelContactPartSettingsTests
{
    [Fact]
    public void Constructor_ShouldDefaultRequireTimeZoneAndDoNotCallToEnabled()
    {
        // Arrange
        var settings = new OmnichannelContactPartSettings();

        // Assert
        Assert.True(settings.RequireTimeZone);
        Assert.True(settings.UseDoNotCall);
        Assert.False(settings.UseDoNotSms);
        Assert.False(settings.UseDoNotChat);
        Assert.False(settings.UseDoNotEmail);
    }
}
