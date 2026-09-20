using CrestApps.Core.ContactCenter;
using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.Core.ContactCenter.Models;
using CrestApps.Core.ContactCenter.Services;
using CrestApps.OrchardCore.ContactCenter.Endpoints;
using CrestApps.OrchardCore.Telephony;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using CrestApps.Core.Telephony;
using CrestApps.Core.Telephony.Models;

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
            EmptyProjectionStore(),
            CreateHttpContext("user-1", new Mock<IRecordingMediaStore>().Object, governance.Object));

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
            EmptyProjectionStore(),
            CreateHttpContext("user-1", new Mock<IRecordingMediaStore>().Object, governance.Object));

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
            EmptyProjectionStore(),
            CreateHttpContext("user-1", mediaStore.Object, governance.Object));

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
            EmptyProjectionStore(),
            CreateHttpContext("user-1", new Mock<IRecordingMediaStore>().Object, governance.Object));

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

    private static DefaultHttpContext CreateHttpContext(
        string userId,
        IRecordingMediaStore mediaStore,
        IRecordingAccessGovernanceService governance)
    {
        var services = new ServiceCollection();
        services.AddSingleton(mediaStore);
        services.AddSingleton(governance);

        return new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId),
            ], "Test")),
            RequestServices = services.BuildServiceProvider(),
        };
    }

    [Fact]
    public async Task HandleVoicemailMediaAsync_WhenTheSoftPhoneRowCarriesItsOwnId_StillFindsTheVoicemail()
    {
        // Arrange
        // The soft phone asks about a voicemail by the id on its own inbox row. That is the platform interaction's
        // id only when the platform projected the row; a call the soft phone sent to voicemail itself carries a
        // generated one. Looked up as an interaction id it found nothing, and the agent was told "not found" about
        // a recording sitting in their inbox — neither playable nor deletable. The row knows the call it was
        // recorded on, and the interaction can be found by that.
        var interaction = CreateInteraction(isVoicemail: true, recipientAgentId: "agent-1", storageReference: "rec-1");
        interaction.ProviderInteractionId = "provider-call-1";

        var (interactionManager, agentManager, governance) = CreateMocks(interaction, agentUserId: "user-1");
        interactionManager
            .Setup(manager => manager.FindByIdAsync("softphone-row-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Interaction)null);
        interactionManager
            .Setup(manager => manager.FindByProviderInteractionIdAsync("provider-call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);
        governance
            .Setup(g => g.RecordAccessAsync("interaction-1", "user-1", "voicemail-playback", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var mediaStore = new Mock<IRecordingMediaStore>();
        mediaStore
            .Setup(store => store.OpenReadAsync("rec-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream([1, 2, 3]));

        var projections = new Mock<ITelephonyInteractionStore>();
        projections
            .Setup(store => store.FindByInteractionIdAsync("user-1", "softphone-row-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelephonyInteraction { InteractionId = "softphone-row-1", CallId = "provider-call-1", UserId = "user-1" });

        // Act
        var result = await AgentWorkspaceEndpoints.HandleVoicemailMediaAsync(
            "softphone-row-1",
            interactionManager.Object,
            agentManager.Object,
            projections.Object,
            CreateHttpContext("user-1", mediaStore.Object, governance.Object));

        // Assert
        Assert.IsNotType<NotFound>(result);
    }

    [Fact]
    public async Task HandleVoicemailMediaAsync_WhenTheRowBelongsToSomebodyElse_IsNotFound()
    {
        // Arrange
        // The fallback reads the inbox row as the signed-in user's own, so an id belonging to another agent
        // resolves to nothing rather than to their voicemail.
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(manager => manager.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Interaction)null);

        var projections = new Mock<ITelephonyInteractionStore>();
        projections
            .Setup(store => store.FindByInteractionIdAsync("user-1", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TelephonyInteraction)null);

        // Act
        var result = await AgentWorkspaceEndpoints.HandleVoicemailMediaAsync(
            "somebody-elses-row",
            interactionManager.Object,
            new Mock<IAgentProfileManager>().Object,
            projections.Object,
            CreateHttpContext("user-1", new Mock<IRecordingMediaStore>().Object, new Mock<IRecordingAccessGovernanceService>().Object));

        // Assert
        Assert.IsType<NotFound>(result);
        interactionManager.Verify(
            manager => manager.FindByProviderInteractionIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static ITelephonyInteractionStore EmptyProjectionStore()
    {
        var store = new Mock<ITelephonyInteractionStore>();
        store.Setup(s => s.FindByInteractionIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TelephonyInteraction)null);

        return store.Object;
    }
}
