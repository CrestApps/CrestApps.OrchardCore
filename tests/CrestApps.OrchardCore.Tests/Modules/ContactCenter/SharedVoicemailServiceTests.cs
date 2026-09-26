using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Tests.Doubles;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Proves what a team can do with the messages in a queue's shared voicemail box, that every request is limited to the
/// queues the user may see, and that every change is recorded.
/// </summary>
public sealed class SharedVoicemailServiceTests
{
    private static readonly DateTime _now = new(2026, 9, 26, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task DeliverAsync_FilesTheMessageOnce_AndRecordsItsArrival()
    {
        // Arrange
        var harness = new Harness();
        var voicemail = new SharedVoicemail
        {
            InteractionId = "interaction-1",
            QueueId = "queue-main",
            CallerNumber = "+15550001000",
            ReceivedUtc = _now.AddMinutes(-2),
        };

        // Act
        var first = await harness.Service.DeliverAsync(voicemail, TestContext.Current.CancellationToken);
        var replayed = await harness.Service.DeliverAsync(
            new SharedVoicemail { InteractionId = "interaction-1", QueueId = "queue-main" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(harness.Voicemails);
        Assert.Same(first, replayed);
        Assert.Equal(SharedVoicemailStatus.New, first.Status);
        Assert.Equal(_now, first.CreatedUtc);
        var audit = Assert.Single(harness.Audit.SharedVoicemails);
        Assert.Equal(ContactCenterConstants.Events.SharedVoicemailReceived, audit.EventType);
        Assert.Equal(first.ItemId, audit.Data.SharedVoicemailId);
        Assert.Equal("queue-main", audit.Data.QueueId);
        Assert.Equal(ContactCenterActor.System, audit.Actor);
    }

    [Fact]
    public async Task ListAsync_ReadsOnlyTheQueuesTheUserMaySee()
    {
        // Arrange
        // The user asked for another team's queue alongside their own: that queue is read from nothing.
        var harness = new Harness(Access(queueIds: ["queue-main"]));

        // Act
        await harness.Service.ListAsync(
            Harness.Principal,
            new SharedVoicemailQuery { QueueIds = ["queue-main", "queue-billing"] },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["queue-main"], harness.LastQuery.QueueIds);
    }

    [Fact]
    public async Task ListAsync_WithoutAQueueFilter_ReadsEveryQueueTheUserMaySee()
    {
        // Arrange
        var harness = new Harness(Access(queueIds: ["queue-main", "queue-support"]));

        // Act
        await harness.Service.ListAsync(Harness.Principal, new SharedVoicemailQuery(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["queue-main", "queue-support"], harness.LastQuery.QueueIds);
    }

    [Fact]
    public async Task ListAsync_ForAUserWhoMayNotUseTheBoxes_ReadsNothing()
    {
        // Arrange
        var harness = new Harness(SharedVoicemailAccess.None);

        // Act
        var page = await harness.Service.ListAsync(Harness.Principal, new SharedVoicemailQuery(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(page.Entries);
        Assert.Null(harness.LastQuery);
    }

    [Fact]
    public async Task EveryRequest_OnAnotherTeamsMessage_IsNotFound()
    {
        // Arrange
        // The message exists, but in a queue the user may not see: they cannot even learn that it is there.
        var harness = new Harness(Access(queueIds: ["queue-main"], canManage: true));
        harness.Add(new SharedVoicemail { ItemId = "vm-1", InteractionId = "interaction-1", QueueId = "queue-billing" });

        // Act
        var results = new[]
        {
            await harness.Service.OpenRecordingAsync(Harness.Principal, "vm-1", TestContext.Current.CancellationToken),
            await harness.Service.ClaimAsync(Harness.Principal, "vm-1", TestContext.Current.CancellationToken),
            await harness.Service.ReleaseAsync(Harness.Principal, "vm-1", TestContext.Current.CancellationToken),
            await harness.Service.ResolveAsync(Harness.Principal, "vm-1", null, TestContext.Current.CancellationToken),
            await harness.Service.RequestCallbackAsync(Harness.Principal, "vm-1", TestContext.Current.CancellationToken),
            await harness.Service.DeleteAsync(Harness.Principal, "vm-1", TestContext.Current.CancellationToken),
        };

        // Assert
        Assert.All(results, result =>
        {
            Assert.Equal(SharedVoicemailActionStatus.NotFound, result.Status);
            Assert.Null(result.Voicemail);
        });
        Assert.Empty(harness.Audit.SharedVoicemails);
        Assert.Equal(SharedVoicemailStatus.New, harness.Voicemails["vm-1"].Status);
    }

    [Fact]
    public async Task ClaimAsync_RecordsWhoIsHandlingTheMessage_AndWhen()
    {
        // Arrange
        var harness = new Harness();
        harness.Add(NewVoicemail());

        // Act
        var result = await harness.Service.ClaimAsync(Harness.Principal, "vm-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(SharedVoicemailStatus.Claimed, result.Voicemail.Status);
        Assert.Equal("user-1", result.Voicemail.ClaimedByUserId);
        Assert.Equal("agent.one", result.Voicemail.ClaimedByUserName);
        Assert.Equal(_now, result.Voicemail.ClaimedUtc);
        Assert.Equal(_now, result.Voicemail.ModifiedUtc);
        Assert.Single(harness.Updates);

        var audit = Assert.Single(harness.Audit.SharedVoicemails);
        Assert.Equal(ContactCenterConstants.Events.SharedVoicemailClaimed, audit.EventType);
        Assert.Equal(SharedVoicemailStatus.New, audit.Data.PreviousStatus);
        Assert.Equal(SharedVoicemailStatus.Claimed, audit.Data.Status);
        Assert.Equal("user-1", audit.Data.UserId);
        Assert.Equal("interaction-1", audit.Data.InteractionId);
        Assert.Equal(ContactCenterActor.Agent("user-1"), audit.Actor);
    }

    [Fact]
    public async Task ClaimAsync_OnAMessageSomebodyElseClaimed_IsRefused_AndTellsWhoHasIt()
    {
        // Arrange
        var harness = new Harness();
        harness.Add(ClaimedVoicemail("user-2", "agent.two"));

        // Act
        var result = await harness.Service.ClaimAsync(Harness.Principal, "vm-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SharedVoicemailActionStatus.Conflict, result.Status);
        Assert.Equal(SharedVoicemailReasons.ClaimedByAnotherUser, result.ReasonCode);
        Assert.Equal("agent.two", result.Voicemail.ClaimedByUserName);
        Assert.Empty(harness.Updates);
        Assert.Empty(harness.Audit.SharedVoicemails);
    }

    [Fact]
    public async Task ClaimAsync_ByAManager_TakesOverSomebodyElsesClaim_AndRecordsWhoHadIt()
    {
        // Arrange
        var harness = new Harness(Access(queueIds: ["queue-main"], canManage: true));
        harness.Add(ClaimedVoicemail("user-2", "agent.two"));

        // Act
        var result = await harness.Service.ClaimAsync(Harness.Principal, "vm-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("user-1", result.Voicemail.ClaimedByUserId);
        var audit = Assert.Single(harness.Audit.SharedVoicemails);
        Assert.Equal("user-2", audit.Data.PreviousClaimedByUserId);
    }

    [Fact]
    public async Task ClaimAsync_OnTheirOwnClaim_ChangesNothing()
    {
        // Arrange
        var harness = new Harness();
        harness.Add(ClaimedVoicemail("user-1", "agent.one"));

        // Act
        var result = await harness.Service.ClaimAsync(Harness.Principal, "vm-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Empty(harness.Updates);
        Assert.Empty(harness.Audit.SharedVoicemails);
    }

    [Fact]
    public async Task ClaimAsync_OnAResolvedMessage_IsRefused()
    {
        // Arrange
        var harness = new Harness();
        var voicemail = ClaimedVoicemail("user-2", "agent.two");
        voicemail.Status = SharedVoicemailStatus.Resolved;
        harness.Add(voicemail);

        // Act
        var result = await harness.Service.ClaimAsync(Harness.Principal, "vm-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SharedVoicemailActionStatus.Conflict, result.Status);
        Assert.Equal(SharedVoicemailReasons.AlreadyResolved, result.ReasonCode);
    }

    [Fact]
    public async Task ReleaseAsync_ReturnsTheirClaimToTheTeam()
    {
        // Arrange
        var harness = new Harness();
        harness.Add(ClaimedVoicemail("user-1", "agent.one"));

        // Act
        var result = await harness.Service.ReleaseAsync(Harness.Principal, "vm-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(SharedVoicemailStatus.New, result.Voicemail.Status);
        Assert.Null(result.Voicemail.ClaimedByUserId);
        Assert.Null(result.Voicemail.ClaimedByUserName);
        Assert.Null(result.Voicemail.ClaimedUtc);

        var audit = Assert.Single(harness.Audit.SharedVoicemails);
        Assert.Equal(ContactCenterConstants.Events.SharedVoicemailReleased, audit.EventType);
        Assert.Equal(SharedVoicemailStatus.Claimed, audit.Data.PreviousStatus);
        Assert.Equal("user-1", audit.Data.PreviousClaimedByUserId);
    }

    [Fact]
    public async Task ReleaseAsync_ReopensAResolvedMessage_AndForgetsHowItWasResolved()
    {
        // Arrange
        var harness = new Harness();
        var voicemail = ClaimedVoicemail("user-1", "agent.one");
        voicemail.Status = SharedVoicemailStatus.Resolved;
        voicemail.ResolvedByUserId = "user-1";
        voicemail.ResolvedByUserName = "agent.one";
        voicemail.ResolvedUtc = _now.AddMinutes(-1);
        voicemail.ResolutionNote = "Called back.";
        harness.Add(voicemail);

        // Act
        var result = await harness.Service.ReleaseAsync(Harness.Principal, "vm-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(SharedVoicemailStatus.New, result.Voicemail.Status);
        Assert.Null(result.Voicemail.ResolvedByUserId);
        Assert.Null(result.Voicemail.ResolvedUtc);
        Assert.Null(result.Voicemail.ResolutionNote);
    }

    [Theory]
    [InlineData(false, SharedVoicemailActionStatus.Forbidden)]
    [InlineData(true, SharedVoicemailActionStatus.Succeeded)]
    public async Task ReleaseAsync_OnSomebodyElsesClaim_NeedsTheManagePermission(bool canManage, SharedVoicemailActionStatus expected)
    {
        // Arrange
        var harness = new Harness(Access(queueIds: ["queue-main"], canManage: canManage));
        harness.Add(ClaimedVoicemail("user-2", "agent.two"));

        // Act
        var result = await harness.Service.ReleaseAsync(Harness.Principal, "vm-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expected, result.Status);
        Assert.Equal(canManage ? SharedVoicemailStatus.New : SharedVoicemailStatus.Claimed, harness.Voicemails["vm-1"].Status);
    }

    [Fact]
    public async Task ResolveAsync_MarksTheMessageDealtWith_WithTheNote_AndClaimsAnUnclaimedMessage()
    {
        // Arrange
        var harness = new Harness();
        harness.Add(NewVoicemail());

        // Act
        var result = await harness.Service.ResolveAsync(Harness.Principal, "vm-1", "  Returned the call, issue sorted.  ", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(SharedVoicemailStatus.Resolved, result.Voicemail.Status);
        Assert.Equal("user-1", result.Voicemail.ResolvedByUserId);
        Assert.Equal("agent.one", result.Voicemail.ResolvedByUserName);
        Assert.Equal(_now, result.Voicemail.ResolvedUtc);
        Assert.Equal("Returned the call, issue sorted.", result.Voicemail.ResolutionNote);
        Assert.Equal("user-1", result.Voicemail.ClaimedByUserId);

        var audit = Assert.Single(harness.Audit.SharedVoicemails);
        Assert.Equal(ContactCenterConstants.Events.SharedVoicemailResolved, audit.EventType);
        Assert.Equal("Returned the call, issue sorted.", audit.Data.Note);
    }

    [Fact]
    public async Task ResolveAsync_WithoutANote_KeepsNoNote()
    {
        // Arrange
        var harness = new Harness();
        harness.Add(ClaimedVoicemail("user-1", "agent.one"));

        // Act
        var result = await harness.Service.ResolveAsync(Harness.Principal, "vm-1", "   ", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Null(result.Voicemail.ResolutionNote);
    }

    [Theory]
    [InlineData(false, SharedVoicemailActionStatus.Forbidden)]
    [InlineData(true, SharedVoicemailActionStatus.Succeeded)]
    public async Task ResolveAsync_OnSomebodyElsesClaim_NeedsTheManagePermission(bool canManage, SharedVoicemailActionStatus expected)
    {
        // Arrange
        var harness = new Harness(Access(queueIds: ["queue-main"], canManage: canManage));
        harness.Add(ClaimedVoicemail("user-2", "agent.two"));

        // Act
        var result = await harness.Service.ResolveAsync(Harness.Principal, "vm-1", null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expected, result.Status);
    }

    [Fact]
    public async Task ResolveAsync_OnAResolvedMessage_IsRefused()
    {
        // Arrange
        var harness = new Harness();
        var voicemail = ClaimedVoicemail("user-1", "agent.one");
        voicemail.Status = SharedVoicemailStatus.Resolved;
        harness.Add(voicemail);

        // Act
        var result = await harness.Service.ResolveAsync(Harness.Principal, "vm-1", "again", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SharedVoicemailActionStatus.Conflict, result.Status);
        Assert.Equal(SharedVoicemailReasons.AlreadyResolved, result.ReasonCode);
    }

    [Fact]
    public async Task RequestCallbackAsync_QueuesACallbackToTheCallerBackIntoTheMessagesQueue()
    {
        // Arrange
        var harness = new Harness();
        var voicemail = NewVoicemail();
        voicemail.ContactContentItemId = "contact-1";
        voicemail.ContactContentType = "Contact";
        harness.Add(voicemail);

        // Act
        var result = await harness.Service.RequestCallbackAsync(Harness.Principal, "vm-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        var callback = Assert.Single(harness.Callbacks);
        Assert.Equal("+15550001000", callback.Destination);
        Assert.Equal("queue-main", callback.QueueId);
        Assert.Equal("contact-1", callback.ContactContentItemId);
        Assert.Equal("Contact", callback.ContactContentType);
        Assert.Equal(_now, callback.ScheduledUtc);
        Assert.False(string.IsNullOrEmpty(callback.ItemId));
        Assert.Same(callback, result.Callback);

        // Asking for the callback is handling the message, so an unclaimed one becomes the user's.
        Assert.Equal(SharedVoicemailStatus.Claimed, result.Voicemail.Status);
        Assert.Equal("user-1", result.Voicemail.ClaimedByUserId);
        Assert.Equal(callback.ItemId, result.Voicemail.CallbackRequestId);
        Assert.Equal("agent.one", result.Voicemail.CallbackRequestedByUserName);
        Assert.Equal(_now, result.Voicemail.CallbackRequestedUtc);

        var audit = Assert.Single(harness.Audit.SharedVoicemails);
        Assert.Equal(ContactCenterConstants.Events.SharedVoicemailCallbackRequested, audit.EventType);
        Assert.Equal(callback.ItemId, audit.Data.CallbackRequestId);
    }

    [Fact]
    public async Task RequestCallbackAsync_WhenCallbacksAreNotEnabled_ChangesNothing()
    {
        // Arrange
        // Without the Outbound Dialer feature the tenant has no callbacks, and scheduling one does nothing.
        var harness = new Harness(callbacksEnabled: false);
        harness.Add(NewVoicemail());

        // Act
        var result = await harness.Service.RequestCallbackAsync(Harness.Principal, "vm-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SharedVoicemailActionStatus.Unavailable, result.Status);
        Assert.Equal(SharedVoicemailReasons.CallbacksUnavailable, result.ReasonCode);
        Assert.Equal(SharedVoicemailStatus.New, harness.Voicemails["vm-1"].Status);
        Assert.Empty(harness.Updates);
        Assert.Empty(harness.Audit.SharedVoicemails);
    }

    [Fact]
    public async Task RequestCallbackAsync_ForACallerWhoLeftNoNumber_IsRefused()
    {
        // Arrange
        var harness = new Harness();
        var voicemail = NewVoicemail();
        voicemail.CallerNumber = null;
        harness.Add(voicemail);

        // Act
        var result = await harness.Service.RequestCallbackAsync(Harness.Principal, "vm-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SharedVoicemailActionStatus.Conflict, result.Status);
        Assert.Equal(SharedVoicemailReasons.NoCallerNumber, result.ReasonCode);
        Assert.Empty(harness.Callbacks);
    }

    [Fact]
    public async Task RequestCallbackAsync_OnSomebodyElsesClaim_IsRefused()
    {
        // Arrange
        var harness = new Harness();
        harness.Add(ClaimedVoicemail("user-2", "agent.two"));

        // Act
        var result = await harness.Service.RequestCallbackAsync(Harness.Principal, "vm-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SharedVoicemailActionStatus.Forbidden, result.Status);
        Assert.Equal(SharedVoicemailReasons.ClaimedByAnotherUser, result.ReasonCode);
        Assert.Empty(harness.Callbacks);
    }

    [Fact]
    public async Task DeleteAsync_WithoutTheManagePermission_IsForbidden()
    {
        // Arrange
        var harness = new Harness();
        harness.Add(ClaimedVoicemail("user-1", "agent.one"));

        // Act
        var result = await harness.Service.DeleteAsync(Harness.Principal, "vm-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SharedVoicemailActionStatus.Forbidden, result.Status);
        Assert.Equal(SharedVoicemailReasons.ManagePermissionRequired, result.ReasonCode);
        Assert.Contains("vm-1", harness.Voicemails.Keys);
        harness.Governance.VerifyNoOtherCalls();
        harness.MediaStore.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DeleteAsync_ByAManager_ErasesTheRecordingThroughGovernance_AndRemovesTheMessage()
    {
        // Arrange
        var harness = new Harness(Access(queueIds: ["queue-main"], canManage: true));
        harness.Add(NewVoicemail());
        harness.Interaction.RecordingReference = "recording-1";
        harness.Interaction.TechnicalMetadata[ContactCenterConstants.RecordingMetadata.StorageReference] = "storage/recording-1";
        harness.Governance
            .Setup(value => value.EraseAsync("interaction-1", ContactCenterActor.Agent("user-1"), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RecordingErasureDecision.Erase());

        // Act
        var result = await harness.Service.DeleteAsync(Harness.Principal, "vm-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.DoesNotContain("vm-1", harness.Voicemails.Keys);
        harness.MediaStore.Verify(value => value.DeleteAsync("storage/recording-1", It.IsAny<CancellationToken>()), Times.Once);
        var audit = Assert.Single(harness.Audit.SharedVoicemails);
        Assert.Equal(ContactCenterConstants.Events.SharedVoicemailDeleted, audit.EventType);
    }

    [Fact]
    public async Task DeleteAsync_ARecordingUnderLegalHold_IsKept()
    {
        // Arrange
        var harness = new Harness(Access(queueIds: ["queue-main"], canManage: true));
        harness.Add(NewVoicemail());
        harness.Interaction.RecordingReference = "recording-1";
        harness.Interaction.TechnicalMetadata[ContactCenterConstants.RecordingMetadata.StorageReference] = "storage/recording-1";
        harness.Governance
            .Setup(value => value.EraseAsync("interaction-1", It.IsAny<ContactCenterActor>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RecordingErasureDecision.Deny(ContactCenterConstants.RecordingErasureDenyReason.LegalHold));

        // Act
        var result = await harness.Service.DeleteAsync(Harness.Principal, "vm-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SharedVoicemailActionStatus.Conflict, result.Status);
        Assert.Equal(SharedVoicemailReasons.LegalHold, result.ReasonCode);
        Assert.Contains("vm-1", harness.Voicemails.Keys);
        harness.MediaStore.Verify(value => value.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Empty(harness.Audit.SharedVoicemails);
    }

    [Fact]
    public async Task OpenRecordingAsync_AuditsTheAccessThroughGovernance_BeforeOpeningTheMedia()
    {
        // Arrange
        var harness = new Harness();
        harness.Add(NewVoicemail());
        harness.Interaction.RecordingReference = "recording-1";
        harness.Interaction.TechnicalMetadata[ContactCenterConstants.RecordingMetadata.StorageReference] = "storage/recording-1";

        var sequence = new List<string>();
        harness.Governance
            .Setup(value => value.RecordAccessAsync("interaction-1", ContactCenterActor.Agent("user-1"), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => sequence.Add("audited"))
            .ReturnsAsync(true);
        var media = new MemoryStream([1, 2, 3]);
        harness.MediaStore
            .Setup(value => value.OpenReadAsync("storage/recording-1", It.IsAny<CancellationToken>()))
            .Callback(() => sequence.Add("opened"))
            .ReturnsAsync(media);

        // Act
        var result = await harness.Service.OpenRecordingAsync(Harness.Principal, "vm-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Same(media, result.Recording);
        Assert.Equal(["audited", "opened"], sequence);
    }

    [Fact]
    public async Task OpenRecordingAsync_WhenGovernanceRefuses_OpensNothing()
    {
        // Arrange
        var harness = new Harness();
        harness.Add(NewVoicemail());
        harness.Interaction.RecordingReference = "recording-1";
        harness.Interaction.TechnicalMetadata[ContactCenterConstants.RecordingMetadata.StorageReference] = "storage/recording-1";
        harness.Governance
            .Setup(value => value.RecordAccessAsync(It.IsAny<string>(), It.IsAny<ContactCenterActor>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await harness.Service.OpenRecordingAsync(Harness.Principal, "vm-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SharedVoicemailActionStatus.Unavailable, result.Status);
        Assert.Equal(SharedVoicemailReasons.NoRecording, result.ReasonCode);
        harness.MediaStore.Verify(value => value.OpenReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OpenRecordingAsync_BeforeTheRecordingIsIngested_IsUnavailable()
    {
        // Arrange
        var harness = new Harness();
        harness.Add(NewVoicemail());

        // Act
        var result = await harness.Service.OpenRecordingAsync(Harness.Principal, "vm-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SharedVoicemailActionStatus.Unavailable, result.Status);
        harness.Governance.VerifyNoOtherCalls();
    }

    private static SharedVoicemailAccess Access(IReadOnlyCollection<string> queueIds, bool canManage = false)
        => new()
        {
            UserId = "user-1",
            UserName = "agent.one",
            CanAccess = true,
            QueueIds = queueIds,
            CanManage = canManage,
        };

    private static SharedVoicemail NewVoicemail()
        => new()
        {
            ItemId = "vm-1",
            InteractionId = "interaction-1",
            QueueId = "queue-main",
            CallerNumber = "+15550001000",
            ReceivedUtc = _now.AddMinutes(-10),
            Status = SharedVoicemailStatus.New,
        };

    private static SharedVoicemail ClaimedVoicemail(string userId, string userName)
    {
        var voicemail = NewVoicemail();
        voicemail.Status = SharedVoicemailStatus.Claimed;
        voicemail.ClaimedByUserId = userId;
        voicemail.ClaimedByUserName = userName;
        voicemail.ClaimedUtc = _now.AddMinutes(-5);

        return voicemail;
    }

    private sealed class Harness
    {
        public static ClaimsPrincipal Principal { get; } = SharedVoicemailAuthorizationServiceTests.CreatePrincipal();

        public Harness(SharedVoicemailAccess access = null, bool callbacksEnabled = true)
        {
            access ??= Access(queueIds: ["queue-main"]);

            var manager = new Mock<ISharedVoicemailManager>();
            manager
                .Setup(value => value.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns((string id, CancellationToken _) => ValueTask.FromResult(Voicemails.GetValueOrDefault(id)));
            manager
                .Setup(value => value.FindByInteractionIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string interactionId, CancellationToken _) => Voicemails.Values.FirstOrDefault(voicemail => voicemail.InteractionId == interactionId));
            manager
                .Setup(value => value.CreateAsync(It.IsAny<SharedVoicemail>(), It.IsAny<CancellationToken>()))
                .Callback((SharedVoicemail voicemail, CancellationToken _) =>
                {
                    voicemail.ItemId ??= $"vm-created-{Voicemails.Count + 1}";
                    Voicemails[voicemail.ItemId] = voicemail;
                });
            manager
                .Setup(value => value.UpdateAsync(It.IsAny<SharedVoicemail>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
                .Callback((SharedVoicemail voicemail, JsonNode _, CancellationToken _) => Updates.Add(voicemail));
            manager
                .Setup(value => value.DeleteAsync(It.IsAny<SharedVoicemail>(), It.IsAny<CancellationToken>()))
                .Callback((SharedVoicemail voicemail, CancellationToken _) => Voicemails.Remove(voicemail.ItemId))
                .ReturnsAsync(true);
            manager
                .Setup(value => value.QueryAsync(It.IsAny<SharedVoicemailQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((SharedVoicemailQuery query, CancellationToken _) =>
                {
                    LastQuery = query;

                    return new SharedVoicemailPage
                    {
                        Entries = Voicemails.Values.Where(voicemail => query.QueueIds is null || query.QueueIds.Contains(voicemail.QueueId)).ToList(),
                    };
                });

            var authorization = new Mock<ISharedVoicemailAuthorizationService>();
            authorization
                .Setup(value => value.GetAccessAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(access);

            var callbacks = new Mock<ICallbackService>();
            callbacks
                .Setup(value => value.ScheduleAsync(It.IsAny<CallbackRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((CallbackRequest callback, CancellationToken _) =>
                {
                    if (!callbacksEnabled)
                    {
                        return null;
                    }

                    Callbacks.Add(callback);

                    return callback;
                });

            var interactions = new Mock<IInteractionManager>();
            interactions
                .Setup(value => value.FindByIdAsync("interaction-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Interaction);

            var clock = new Mock<IClock>();
            clock.SetupGet(value => value.UtcNow).Returns(_now);

            Service = new SharedVoicemailService(
                manager.Object,
                authorization.Object,
                Audit,
                callbacks.Object,
                interactions.Object,
                [Governance.Object],
                [MediaStore.Object],
                clock.Object);
        }

        public Dictionary<string, SharedVoicemail> Voicemails { get; } = [];

        public List<SharedVoicemail> Updates { get; } = [];

        public List<CallbackRequest> Callbacks { get; } = [];

        public SharedVoicemailQuery LastQuery { get; private set; }

        public Interaction Interaction { get; } = new() { ItemId = "interaction-1" };

        public RecordingContactCenterAuditRecorder Audit { get; } = new();

        public Mock<IRecordingAccessGovernanceService> Governance { get; } = new();

        public Mock<IRecordingMediaStore> MediaStore { get; } = new();

        public SharedVoicemailService Service { get; }

        public void Add(SharedVoicemail voicemail)
            => Voicemails[voicemail.ItemId] = voicemail;
    }
}
