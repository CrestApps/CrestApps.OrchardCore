using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Endpoints;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class VoicemailMediaEndpointTests
{
    [Fact]
    public async Task HandleVoicemailMediaAsync_WhenNotAVoicemail_ReturnsNotFound()
    {
        var interaction = CreateInteraction(isVoicemail: false, recipientAgentId: "agent-1", storageReference: "rec-1");
        var (interactionManager, agentManager, governance) = CreateMocks(interaction, agentUserId: "user-1");

        var result = await AgentWorkspaceEndpoints.HandleVoicemailMediaAsync(
            "interaction-1",
            interactionManager.Object,
            agentManager.Object,
            governance.Object,
            CreateHttpContext("user-1", new Mock<IRecordingMediaStore>().Object));

        Assert.IsType<NotFound>(result);
        governance.Verify(g => g.RecordAccessAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleVoicemailMediaAsync_WhenNotTheRecipient_ReturnsForbid()
    {
        var interaction = CreateInteraction(isVoicemail: true, recipientAgentId: "agent-1", storageReference: "rec-1");
        // The recipient agent belongs to a different user than the caller.
        var (interactionManager, agentManager, governance) = CreateMocks(interaction, agentUserId: "someone-else");

        var result = await AgentWorkspaceEndpoints.HandleVoicemailMediaAsync(
            "interaction-1",
            interactionManager.Object,
            agentManager.Object,
            governance.Object,
            CreateHttpContext("user-1", new Mock<IRecordingMediaStore>().Object));

        Assert.IsType<ForbidHttpResult>(result);
        governance.Verify(g => g.RecordAccessAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleVoicemailMediaAsync_WhenRecipientAndReady_AuditsAndStreamsMedia()
    {
        var interaction = CreateInteraction(isVoicemail: true, recipientAgentId: "agent-1", storageReference: "rec-1");
        var (interactionManager, agentManager, governance) = CreateMocks(interaction, agentUserId: "user-1");
        governance
            .Setup(g => g.RecordAccessAsync("interaction-1", "user-1", "voicemail-playback", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var mediaStore = new Mock<IRecordingMediaStore>();
        mediaStore
            .Setup(store => store.OpenReadAsync("rec-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream([1, 2, 3]));

        var result = await AgentWorkspaceEndpoints.HandleVoicemailMediaAsync(
            "interaction-1",
            interactionManager.Object,
            agentManager.Object,
            governance.Object,
            CreateHttpContext("user-1", mediaStore.Object));

        var fileResult = Assert.IsType<FileStreamHttpResult>(result);
        Assert.Equal("audio/mpeg", fileResult.ContentType);
        governance.Verify(g => g.RecordAccessAsync("interaction-1", "user-1", "voicemail-playback", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleVoicemailMediaAsync_WhenRecordingNotYetIngested_ReturnsNotFoundWithoutAudit()
    {
        // A voicemail whose recording has not been correlated yet has no storage reference.
        var interaction = CreateInteraction(isVoicemail: true, recipientAgentId: "agent-1", storageReference: null);
        var (interactionManager, agentManager, governance) = CreateMocks(interaction, agentUserId: "user-1");

        var result = await AgentWorkspaceEndpoints.HandleVoicemailMediaAsync(
            "interaction-1",
            interactionManager.Object,
            agentManager.Object,
            governance.Object,
            CreateHttpContext("user-1", new Mock<IRecordingMediaStore>().Object));

        Assert.IsType<NotFound>(result);
        governance.Verify(g => g.RecordAccessAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static (Mock<IInteractionManager>, Mock<IAgentProfileManager>, Mock<IRecordingAccessGovernanceService>) CreateMocks(
        Interaction interaction,
        string agentUserId)
    {
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(manager => manager.FindByIdAsync("interaction-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);

        var agentManager = new Mock<IAgentProfileManager>();
        agentManager
            .Setup(manager => manager.FindByIdAsync("agent-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile { ItemId = "agent-1", UserId = agentUserId });

        var governance = new Mock<IRecordingAccessGovernanceService>();

        return (interactionManager, agentManager, governance);
    }

    private static Interaction CreateInteraction(bool isVoicemail, string recipientAgentId, string storageReference)
    {
        var metadata = new Dictionary<string, object>
        {
            [ContactCenterConstants.Voicemail.RecipientAgentMetadataKey] = recipientAgentId,
        };

        if (isVoicemail)
        {
            metadata[ContactCenterConstants.Voicemail.ProjectionMetadataKey] = true;
        }

        if (!string.IsNullOrEmpty(storageReference))
        {
            metadata[ContactCenterConstants.RecordingMetadata.StorageReference] = storageReference;
        }

        return new Interaction
        {
            ItemId = "interaction-1",
            AgentId = recipientAgentId,
            RecordingReference = string.IsNullOrEmpty(storageReference) ? null : storageReference,
            TechnicalMetadata = metadata,
        };
    }

    private static DefaultHttpContext CreateHttpContext(string userId, IRecordingMediaStore mediaStore)
    {
        var services = new ServiceCollection();
        services.AddSingleton(mediaStore);

        return new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId),
            ], "Test")),
            RequestServices = services.BuildServiceProvider(),
        };
    }
}
