using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Moq;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Services;

public sealed class AutomatedVoiceActivitySettingsResolverTests
{
    [Fact]
    public async Task ResolveAsync_WhenActivityOverridesAreSet_ShouldUseActivityValues()
    {
        // Arrange
        var activity = new OmnichannelActivity
        {
            SubjectContentType = "Appointment",
            AIProfileId = "activity-profile",
            SpeechToTextDeploymentName = "activity-stt",
            TextToSpeechDeploymentName = "activity-tts",
            TextToSpeechVoiceId = "activity-voice",
        };
        var flowSettings = CreateFlowSettings();
        var resolver = CreateResolver(flowSettings, CreateSiteSettings());

        // Act
        var result = await resolver.ResolveAsync(activity, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("activity-profile", result.AIProfileId);
        Assert.Equal("activity-stt", result.SpeechToTextDeploymentName);
        Assert.Equal("activity-tts", result.TextToSpeechDeploymentName);
        Assert.Equal("activity-voice", result.TextToSpeechVoiceId);
    }

    [Fact]
    public async Task ResolveAsync_WhenActivityAndFlowSpeechSettingsAreBlank_ShouldUseSiteDefaults()
    {
        // Arrange
        var activity = new OmnichannelActivity
        {
            SubjectContentType = "Appointment",
        };
        var flowSettings = new SubjectFlowSettings
        {
            ProfileId = "flow-profile",
        };
        var resolver = CreateResolver(flowSettings, CreateSiteSettings());

        // Act
        var result = await resolver.ResolveAsync(activity, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("flow-profile", result.AIProfileId);
        Assert.Equal("site-stt", result.SpeechToTextDeploymentName);
        Assert.Equal("site-tts", result.TextToSpeechDeploymentName);
        Assert.Equal("site-voice", result.TextToSpeechVoiceId);
    }

    private static AutomatedVoiceActivitySettingsResolver CreateResolver(
        SubjectFlowSettings flowSettings,
        DefaultAIDeploymentSettings deploymentSettings)
    {
        var flowSettingsService = new Mock<ISubjectFlowSettingsService>();
        flowSettingsService
            .Setup(service => service.FindConfiguredFlowSettingsAsync("Appointment", It.IsAny<CancellationToken>()))
            .ReturnsAsync(flowSettings);

        var site = new Mock<ISite>();
        site.Setup(currentSite => currentSite.GetOrCreate<DefaultAIDeploymentSettings>())
            .Returns(deploymentSettings);

        var siteService = new Mock<ISiteService>();
        siteService.Setup(service => service.GetSiteSettingsAsync())
            .ReturnsAsync(site.Object);

        return new AutomatedVoiceActivitySettingsResolver(flowSettingsService.Object, siteService.Object);
    }

    private static SubjectFlowSettings CreateFlowSettings()
    {
        return new SubjectFlowSettings
        {
            ProfileId = "flow-profile",
            SpeechToTextDeploymentName = "flow-stt",
            TextToSpeechDeploymentName = "flow-tts",
            TextToSpeechVoiceId = "flow-voice",
        };
    }

    private static DefaultAIDeploymentSettings CreateSiteSettings()
    {
        return new DefaultAIDeploymentSettings
        {
            DefaultSpeechToTextDeploymentName = "site-stt",
            DefaultTextToSpeechDeploymentName = "site-tts",
            DefaultTextToSpeechVoiceId = "site-voice",
        };
    }
}
