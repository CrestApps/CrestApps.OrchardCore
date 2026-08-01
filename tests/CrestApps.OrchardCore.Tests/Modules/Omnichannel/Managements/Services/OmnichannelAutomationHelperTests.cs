using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Services;

public sealed class OmnichannelAutomationHelperTests
{
    [Fact]
    public void ResolveActivitySettings_WhenBatchOverridesAreSet_ShouldUseBatchValues()
    {
        // Arrange
        var batch = new OmnichannelActivityBatch
        {
            AIProfileId = "batch-profile",
            SpeechToTextDeploymentName = "batch-stt",
            TextToSpeechDeploymentName = "batch-tts",
            TextToSpeechVoiceId = "batch-voice",
        };
        var flowSettings = new SubjectFlowSettings
        {
            ProfileId = "flow-profile",
            SpeechToTextDeploymentName = "flow-stt",
            TextToSpeechDeploymentName = "flow-tts",
            TextToSpeechVoiceId = "flow-voice",
        };

        // Act
        var result = OmnichannelAutomationHelper.ResolveActivitySettings(batch, flowSettings);

        // Assert
        Assert.Equal("batch-profile", result.AIProfileId);
        Assert.Equal("batch-stt", result.SpeechToTextDeploymentName);
        Assert.Equal("batch-tts", result.TextToSpeechDeploymentName);
        Assert.Equal("batch-voice", result.TextToSpeechVoiceId);
    }

    [Fact]
    public void ResolveActivitySettings_WhenBatchOverridesAreBlank_ShouldUseSubjectFlowValues()
    {
        // Arrange
        var batch = new OmnichannelActivityBatch();
        var flowSettings = new SubjectFlowSettings
        {
            ProfileId = "flow-profile",
            SpeechToTextDeploymentName = "flow-stt",
            TextToSpeechDeploymentName = "flow-tts",
            TextToSpeechVoiceId = "flow-voice",
        };

        // Act
        var result = OmnichannelAutomationHelper.ResolveActivitySettings(batch, flowSettings);

        // Assert
        Assert.Equal("flow-profile", result.AIProfileId);
        Assert.Equal("flow-stt", result.SpeechToTextDeploymentName);
        Assert.Equal("flow-tts", result.TextToSpeechDeploymentName);
        Assert.Equal("flow-voice", result.TextToSpeechVoiceId);
    }
}
