using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Endpoints;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
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
        var interaction = CreateInteraction(isVoicemail: false, storageReference: "rec-1");
        var (interactionManager, governance) = CreateMocks(interaction);

        var result = await AgentWorkspaceEndpoints.HandleVoicemailMediaAsync(
            "interaction-1",
            interactionManager.Object,
            InboxStore(new TelephonyInteraction { InteractionId = "interaction-1", CallId = "provider-call-1", UserId = "user-1", IsVoicemail = true }),
            CreateHttpContext("user-1", new Mock<IRecordingMediaStore>().Object, governance.Object));

        Assert.IsType<NotFound>(result);
        governance.Verify(g => g.RecordAccessAsync(It.IsAny<string>(), It.IsAny<ContactCenterActor>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleVoicemailMediaAsync_WhenNotInTheCallersInbox_IsForbidden()
    {
        var interaction = CreateInteraction(isVoicemail: true, storageReference: "rec-1");
        var (interactionManager, governance) = CreateMocks(interaction);

        var result = await AgentWorkspaceEndpoints.HandleVoicemailMediaAsync(
            "interaction-1",
            interactionManager.Object,
            InboxStore(),
            CreateHttpContext("user-1", new Mock<IRecordingMediaStore>().Object, governance.Object));

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ProblemHttpResult>(result).StatusCode);
        governance.Verify(g => g.RecordAccessAsync(It.IsAny<string>(), It.IsAny<ContactCenterActor>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleVoicemailMediaAsync_WhenInTheCallersInboxAndReady_AuditsAsTheAgentAndStreamsMedia()
    {
        var interaction = CreateInteraction(isVoicemail: true, storageReference: "rec-1");
        var (interactionManager, governance) = CreateMocks(interaction);
        governance
            .Setup(g => g.RecordAccessAsync("interaction-1", ContactCenterActor.Agent("user-1"), "voicemail-playback", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var mediaStore = new Mock<IRecordingMediaStore>();
        mediaStore
            .Setup(store => store.OpenReadAsync("rec-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream([1, 2, 3]));

        var result = await AgentWorkspaceEndpoints.HandleVoicemailMediaAsync(
            "interaction-1",
            interactionManager.Object,
            InboxStore(new TelephonyInteraction { InteractionId = "interaction-1", CallId = "provider-call-1", UserId = "user-1", IsVoicemail = true }),
            CreateHttpContext("user-1", mediaStore.Object, governance.Object));

        var fileResult = Assert.IsType<FileStreamHttpResult>(result);
        Assert.Equal("audio/mpeg", fileResult.ContentType);
        governance.Verify(g => g.RecordAccessAsync("interaction-1", ContactCenterActor.Agent("user-1"), "voicemail-playback", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleVoicemailMediaAsync_WhenRecordingNotYetIngested_ReturnsNotFoundWithoutAudit()
    {
        // A voicemail whose recording has not been correlated yet has no storage reference.
        var interaction = CreateInteraction(isVoicemail: true, storageReference: null);
        var (interactionManager, governance) = CreateMocks(interaction);

        var result = await AgentWorkspaceEndpoints.HandleVoicemailMediaAsync(
            "interaction-1",
            interactionManager.Object,
            InboxStore(new TelephonyInteraction { InteractionId = "interaction-1", CallId = "provider-call-1", UserId = "user-1", IsVoicemail = true }),
            CreateHttpContext("user-1", new Mock<IRecordingMediaStore>().Object, governance.Object));

        Assert.IsType<NotFound>(result);
        governance.Verify(g => g.RecordAccessAsync(It.IsAny<string>(), It.IsAny<ContactCenterActor>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
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
        var interaction = CreateInteraction(isVoicemail: true, storageReference: "rec-1");

        var (interactionManager, governance) = CreateMocks(interaction);
        interactionManager
            .Setup(manager => manager.FindByIdAsync("softphone-row-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Interaction)null);
        interactionManager
            .Setup(manager => manager.FindByProviderInteractionIdAsync("provider-call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);
        governance
            .Setup(g => g.RecordAccessAsync("interaction-1", It.IsAny<ContactCenterActor>(), "voicemail-playback", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var mediaStore = new Mock<IRecordingMediaStore>();
        mediaStore
            .Setup(store => store.OpenReadAsync("rec-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream([1, 2, 3]));

        // Act
        var result = await AgentWorkspaceEndpoints.HandleVoicemailMediaAsync(
            "softphone-row-1",
            interactionManager.Object,
            InboxStore(new TelephonyInteraction { InteractionId = "softphone-row-1", CallId = "provider-call-1", UserId = "user-1", IsVoicemail = true }),
            CreateHttpContext("user-1", mediaStore.Object, governance.Object));

        // Assert
        Assert.IsType<FileStreamHttpResult>(result);
    }

    [Fact]
    public async Task HandleVoicemailMediaAsync_WhenTheRowBelongsToSomebodyElse_IsNotFound()
    {
        // Arrange
        // The inbox row is read as the signed-in user's own, so an id belonging to another agent resolves to nothing
        // rather than to their voicemail.
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(manager => manager.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Interaction)null);

        // Act
        var result = await AgentWorkspaceEndpoints.HandleVoicemailMediaAsync(
            "somebody-elses-row",
            interactionManager.Object,
            InboxStore(),
            CreateHttpContext("user-1", new Mock<IRecordingMediaStore>().Object, new Mock<IRecordingAccessGovernanceService>().Object));

        // Assert
        Assert.IsType<NotFound>(result);
        interactionManager.Verify(
            manager => manager.FindByProviderInteractionIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static (Mock<IInteractionManager>, Mock<IRecordingAccessGovernanceService>) CreateMocks(Interaction interaction)
    {
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(manager => manager.FindByIdAsync("interaction-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);

        return (interactionManager, new Mock<IRecordingAccessGovernanceService>());
    }

    private static Interaction CreateInteraction(bool isVoicemail, string storageReference)
    {
        var metadata = new Dictionary<string, object>();

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
            ProviderInteractionId = "provider-call-1",
            RecordingReference = string.IsNullOrEmpty(storageReference) ? null : storageReference,
            TechnicalMetadata = metadata,
        };
    }

    /// <summary>
    /// A soft-phone inbox holding the given rows, answering the lookups the way the real store does: always as the
    /// named user's own.
    /// </summary>
    private static ITelephonyInteractionStore InboxStore(params TelephonyInteraction[] rows)
    {
        var store = new Mock<ITelephonyInteractionStore>();
        store
            .Setup(s => s.FindByInteractionIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string userId, string interactionId, CancellationToken _) =>
                rows.FirstOrDefault(row => row.UserId == userId && row.InteractionId == interactionId));
        store
            .Setup(s => s.FindByCallIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string userId, string callId, CancellationToken _) =>
                rows.FirstOrDefault(row => row.UserId == userId && row.CallId == callId));

        return store.Object;
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
}
