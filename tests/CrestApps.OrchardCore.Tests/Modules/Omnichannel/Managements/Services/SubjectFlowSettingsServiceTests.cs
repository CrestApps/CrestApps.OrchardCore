using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Services;

public sealed class SubjectFlowSettingsServiceTests
{
    [Fact]
    public void IsConfigured_WhenAutomatedFlowHasProfileAndEndpoint_ShouldReturnTrue()
    {
        // Arrange
        var service = new SubjectFlowSettingsService(null, null);
        var flowSettings = CreateAutomatedFlowSettings();

        // Act
        var result = service.IsConfigured(flowSettings);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsConfigured_WhenAutomatedFlowMissingProfile_ShouldReturnFalse()
    {
        // Arrange
        var service = new SubjectFlowSettingsService(null, null);
        var flowSettings = CreateAutomatedFlowSettings();
        flowSettings.ProfileId = null;

        // Act
        var result = service.IsConfigured(flowSettings);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsConfigured_WhenManualFlowHasNoProfile_ShouldReturnTrue()
    {
        // Arrange
        var service = new SubjectFlowSettingsService(null, null);
        var flowSettings = new SubjectFlowSettings
        {
            SubjectContentType = "Renewal",
            CampaignId = "campaign-1",
            Channel = OmnichannelConstants.Channels.Phone,
            InteractionType = ActivityInteractionType.Manual,
        };

        // Act
        var result = service.IsConfigured(flowSettings);

        // Assert
        Assert.True(result);
    }

    private static SubjectFlowSettings CreateAutomatedFlowSettings()
    {
        return new SubjectFlowSettings
        {
            SubjectContentType = "Renewal",
            CampaignId = "campaign-1",
            Channel = OmnichannelConstants.Channels.Sms,
            ChannelEndpointId = "endpoint-1",
            InteractionType = ActivityInteractionType.Automated,
            ProfileId = "profile-1",
        };
    }
}
