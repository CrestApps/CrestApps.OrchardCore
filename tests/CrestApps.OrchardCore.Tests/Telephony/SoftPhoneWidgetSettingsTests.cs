using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class SoftPhoneWidgetSettingsTests
{
    [Fact]
    public void Constructor_WithoutConfiguration_ShouldDefaultRecentCallsCountToThirty()
    {
        // Act
        var settings = new SoftPhoneWidgetSettings();

        // Assert
        Assert.Equal(30, settings.RecentCallsCount);
        Assert.Equal(SoftPhoneWidgetSettings.DefaultRecentCallsCount, settings.RecentCallsCount);
    }
}
