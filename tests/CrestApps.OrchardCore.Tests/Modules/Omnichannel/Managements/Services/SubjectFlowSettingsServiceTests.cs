using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Services;

public sealed class SubjectFlowSettingsServiceTests
{
    [Fact]
    public void IsConfigured_WhenFlowHasSubjectContentType_ShouldReturnTrue()
    {
        // Arrange
        var service = new SubjectFlowSettingsService(null);
        var flowSettings = CreateAutomatedFlowSettings();

        // Act
        var result = service.IsConfigured(flowSettings);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsConfigured_WhenFlowIsNull_ShouldReturnFalse()
    {
        // Arrange
        var service = new SubjectFlowSettingsService(null);

        // Act
        var result = service.IsConfigured(null);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsConfigured_WhenSubjectContentTypeIsMissing_ShouldReturnFalse(string subjectContentType)
    {
        // Arrange
        var service = new SubjectFlowSettingsService(null);
        var flowSettings = CreateAutomatedFlowSettings();
        flowSettings.SubjectContentType = subjectContentType;

        // Act
        var result = service.IsConfigured(flowSettings);

        // Assert
        Assert.False(result);
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
