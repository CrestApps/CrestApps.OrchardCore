using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// The bookkeeping that runs after a call was sent to voicemail: the agent is reloaded, the provider copy is
/// recorded once, and a save that fails is logged, so the next call uploads the greeting again rather than
/// anybody being paged.
/// </summary>
public sealed class DeferredVoicemailGreetingCopyRecorderTests
{
    [Fact]
    public async Task RecordAsync_ReloadsTheAgent_RecordsTheCopy_AndSaves()
    {
        // Arrange
        var agent = new AgentProfile
        {
            ItemId = "agent-9",
            VoicemailGreetingMedia = new VoiceMediaAsset
            {
                MasterStorageReference = "voice-media-greeting",
                ProviderReferences = new Dictionary<string, string> { ["Telnyx"] = "cc-vm-greeting-telnyx" },
            },
        };
        var agents = CreateAgents(agent);

        // Act
        await DeferredVoicemailGreetingCopyRecorder.RecordAsync(CreateServices(agents), "agent-9", "OtherProvider", "cc-vm-greeting-other");

        // Assert
        Assert.Equal("cc-vm-greeting-other", agent.VoicemailGreetingMedia.ProviderReferences["OtherProvider"]);
        Assert.Equal("cc-vm-greeting-telnyx", agent.VoicemailGreetingMedia.ProviderReferences["Telnyx"]);
        agents.Verify(manager => manager.UpdateAsync(agent, It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecordAsync_WhenTheCopyIsAlreadyRecorded_DoesNotSave()
    {
        // Arrange
        // Two calls reached the voicemail before the first bookkeeping ran; the second finds nothing to do.
        var agent = new AgentProfile
        {
            ItemId = "agent-9",
            VoicemailGreetingMedia = new VoiceMediaAsset
            {
                ProviderReferences = new Dictionary<string, string> { ["otherprovider"] = "cc-vm-greeting-other" },
            },
        };
        var agents = CreateAgents(agent);

        // Act
        await DeferredVoicemailGreetingCopyRecorder.RecordAsync(CreateServices(agents), "agent-9", "OtherProvider", "cc-vm-greeting-other");

        // Assert
        agents.Verify(manager => manager.UpdateAsync(It.IsAny<AgentProfile>(), It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RecordAsync_WhenTheGreetingWasRemovedMeanwhile_DoesNotResurrectIt()
    {
        // Arrange
        var agent = new AgentProfile { ItemId = "agent-9" };
        var agents = CreateAgents(agent);

        // Act
        await DeferredVoicemailGreetingCopyRecorder.RecordAsync(CreateServices(agents), "agent-9", "OtherProvider", "cc-vm-greeting-other");

        // Assert
        Assert.Null(agent.VoicemailGreetingMedia);
        agents.Verify(manager => manager.UpdateAsync(It.IsAny<AgentProfile>(), It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RecordAsync_WhenTheSaveFails_LogsAndDoesNotThrow()
    {
        // Arrange
        var agent = new AgentProfile
        {
            ItemId = "agent-9",
            VoicemailGreetingMedia = new VoiceMediaAsset(),
        };
        var agents = CreateAgents(agent);
        agents
            .Setup(manager => manager.UpdateAsync(It.IsAny<AgentProfile>(), It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("The document was changed by somebody else."));

        // Act
        var exception = await Record.ExceptionAsync(() => DeferredVoicemailGreetingCopyRecorder.RecordAsync(CreateServices(agents), "agent-9", "OtherProvider", "cc-vm-greeting-other"));

        // Assert
        Assert.Null(exception);
    }

    private static Mock<IAgentProfileManager> CreateAgents(AgentProfile agent)
    {
        var agents = new Mock<IAgentProfileManager>();
        agents.Setup(manager => manager.FindByIdAsync(agent.ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(agent);
        agents
            .Setup(manager => manager.UpdateAsync(It.IsAny<AgentProfile>(), It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        return agents;
    }

    private static ServiceProvider CreateServices(Mock<IAgentProfileManager> agents)
        => new ServiceCollection()
            .AddLogging()
            .AddSingleton(agents.Object)
            .BuildServiceProvider();
}
