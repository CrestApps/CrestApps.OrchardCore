using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.AI;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Tests.Agent;
using Moq;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.AITools;

/// <summary>
/// The voice media, call recording and shared voicemail tools go through the services the admin pages use: the own-calls
/// rule and the access audit hold for recordings, and the shared voicemail service decides every voicemail action.
/// </summary>
public sealed class ContactCenterRecordingAndVoicemailToolTests
{
    private const string CallerId = AgentToolTestContext.UserId;

    [Fact]
    public async Task ListVoiceMedia_ListsClipsWithoutTheirStorageReferences()
    {
        // Arrange
        var harness = new ContactCenterOperationsToolHarness();
        harness.VoiceMedia.Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new VoiceMediaItem { ItemId = "clip-1", Name = "Main welcome", Format = "mp3", MasterStorageReference = "secret/path", ProviderReferences = new() { ["Telnyx"] = "ref-1" } },
            new VoiceMediaItem { ItemId = "clip-2", Name = "Hold music", Format = "wav" },
        ]);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListVoiceMediaTool(), harness.Build(), new() { ["missingProviderCopy"] = true });
        var json = AgentToolTestContext.ReadJson(result);

        // Assert
        var item = Assert.Single(json.GetProperty("items").EnumerateArray());
        Assert.Equal("clip-2", item.GetProperty("id").GetString());
        Assert.False(item.GetProperty("hasAudio").GetBoolean());
        Assert.DoesNotContain("secret/path", result);
        Assert.DoesNotContain("ref-1", result);
    }

    [Fact]
    public async Task SaveVoiceMedia_RenamesAClipThroughTheManager()
    {
        // Arrange
        var harness = new ContactCenterOperationsToolHarness();
        var clip = new VoiceMediaItem { ItemId = "clip-1", Name = "Main welcome" };
        harness.VoiceMedia.Setup(manager => manager.FindByIdAsync("clip-1", It.IsAny<CancellationToken>())).ReturnsAsync(clip);
        harness.VoiceMedia.Setup(manager => manager.ValidateAsync(clip, It.IsAny<CancellationToken>())).ReturnsAsync(new CrestApps.Core.Models.ValidationResultDetails());

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveVoiceMediaTool(), harness.Build(), new() { ["id"] = "clip-1", ["name"] = "Holiday greeting" });

        // Assert
        Assert.True(AgentToolTestContext.ReadJson(result).GetProperty("saved").GetBoolean());
        Assert.Equal("Holiday greeting", clip.Name);
        harness.VoiceMedia.Verify(manager => manager.UpdateAsync(clip, null, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Empty(harness.Provisioning.Invocations);
    }

    [Fact]
    public async Task SaveVoiceMedia_RefusesAudioWhenTheMediaLibraryIsMissing()
    {
        // Arrange
        var harness = new ContactCenterOperationsToolHarness();
        var clip = new VoiceMediaItem { ItemId = "clip-1", Name = "Main welcome" };
        harness.VoiceMedia.Setup(manager => manager.FindByIdAsync("clip-1", It.IsAny<CancellationToken>())).ReturnsAsync(clip);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveVoiceMediaTool(), harness.Build(), new() { ["id"] = "clip-1", ["mediaPath"] = "audio/holiday.wav" });

        // Assert
        Assert.StartsWith("Nothing was saved: the media library is not available", result);
        Assert.Equal("Main welcome", clip.Name);
        harness.VoiceMedia.Verify(manager => manager.UpdateAsync(It.IsAny<VoiceMediaItem>(), It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Empty(harness.Provisioning.Invocations);
    }

    [Fact]
    public async Task ResyncVoiceMedia_ReportsEachProvider()
    {
        // Arrange
        var harness = new ContactCenterOperationsToolHarness();
        var clip = new VoiceMediaItem { ItemId = "clip-1", Name = "Main welcome" };
        harness.VoiceMedia.Setup(manager => manager.FindByIdAsync("clip-1", It.IsAny<CancellationToken>())).ReturnsAsync(clip);
        harness.Provisioning.Setup(provisioning => provisioning.ResyncAsync(clip, It.IsAny<CancellationToken>())).ReturnsAsync(new VoiceMediaResyncResult
        {
            HasMasterCopy = true,
            Providers = new Dictionary<string, VoiceMediaResyncOutcome> { ["Telnyx"] = VoiceMediaResyncOutcome.Uploaded },
        });

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ResyncVoiceMediaTool(), harness.Build(), new() { ["id"] = "clip-1" });

        // Assert
        var provider = Assert.Single(AgentToolTestContext.ReadJson(result).GetProperty("providers").EnumerateArray());
        Assert.Equal("uploaded", provider.GetProperty("outcome").GetString());
    }

    [Fact]
    public async Task ResyncVoiceMedia_ReportsAnUnknownClip()
    {
        // Arrange
        var harness = new ContactCenterOperationsToolHarness();
        harness.VoiceMedia.Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ResyncVoiceMediaTool(), harness.Build(), new() { ["id"] = "missing" });

        // Assert
        Assert.StartsWith("Nothing was re-synced: no voice media clip", result);
        Assert.Empty(harness.Provisioning.Invocations);
    }

    [Fact]
    public async Task ListCallRecordings_SearchesOnlyTheCallersOwnCallsWithoutTheAllRecordingsPermission()
    {
        // Arrange
        var harness = new ContactCenterOperationsToolHarness();
        CallRecordingQuery query = null;
        harness.Recordings.Setup(store => store.QueryAsync(It.IsAny<CallRecordingQuery>(), It.IsAny<CancellationToken>()))
            .Callback((CallRecordingQuery value, CancellationToken _) => query = value)
            .ReturnsAsync(new CallRecordingPage
            {
                Count = 1,
                Entries = [new CallRecording { ItemId = "rec-1", AgentUserId = CallerId, CustomerAddress = "+15550104455", StartedUtc = ContactCenterOperationsToolHarness.Now }],
            });

        // Act
        var result = await AgentToolTestContext.InvokeAsync(
            new ListCallRecordingsTool(),
            harness.Build(authorize: OwnRecordingsOnly),
            new() { ["agentUserId"] = "someone-else", ["customerNumber"] = "+1 555 010 4455" });

        // Assert
        Assert.Equal(CallerId, query.AgentUserId);
        Assert.Equal("+15550104455", query.CustomerAddress);
        var json = AgentToolTestContext.ReadJson(result);
        Assert.True(json.GetProperty("onlyOwnCalls").GetBoolean());
        Assert.Equal("4455", Assert.Single(json.GetProperty("items").EnumerateArray()).GetProperty("customerNumberEnding").GetString());
        Assert.DoesNotContain("+15550104455", result);
    }

    [Fact]
    public async Task GetCallRecording_AuditsTheReadAndReturnsTheRecording()
    {
        // Arrange
        var harness = new ContactCenterOperationsToolHarness();
        AddRecording(harness, "rec-1", CallerId, "interaction-1");
        harness.Governance.Setup(governance => governance.RecordAccessAsync("interaction-1", It.IsAny<ContactCenterActor>(), GetCallRecordingTool.AuditPurpose, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetCallRecordingTool(), harness.Build(authorize: OwnRecordingsOnly), new() { ["id"] = "rec-1" });

        // Assert
        Assert.Equal("rec-1", AgentToolTestContext.ReadJson(result).GetProperty("id").GetString());
        harness.Governance.Verify(governance => governance.RecordAccessAsync("interaction-1", It.IsAny<ContactCenterActor>(), GetCallRecordingTool.AuditPurpose, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetCallRecording_ReportsAnotherAgentsRecordingAsNotFound()
    {
        // Arrange
        var harness = new ContactCenterOperationsToolHarness();
        AddRecording(harness, "rec-2", "user-other", "interaction-2");

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetCallRecordingTool(), harness.Build(authorize: OwnRecordingsOnly), new() { ["id"] = "rec-2" });

        // Assert
        Assert.StartsWith("No recording you may hear has the id 'rec-2'", result);
        Assert.Empty(harness.Governance.Invocations);
    }

    [Fact]
    public async Task ShareCallRecording_ReturnsASignedLinkAndAuditsIt()
    {
        // Arrange
        var harness = new ContactCenterOperationsToolHarness();
        AddRecording(harness, "rec-1", "user-other", "interaction-1");
        var services = harness.Build();
        var httpContext = services.GetService(typeof(Microsoft.AspNetCore.Http.IHttpContextAccessor)) as Microsoft.AspNetCore.Http.IHttpContextAccessor;
        httpContext.HttpContext.Request.Scheme = "https";
        httpContext.HttpContext.Request.Host = new Microsoft.AspNetCore.Http.HostString("example.test");

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ShareCallRecordingTool(), services, new() { ["id"] = "rec-1" });

        // Assert
        Assert.StartsWith("https://example.test/call-recordings/shared/", AgentToolTestContext.ReadJson(result).GetProperty("url").GetString());
        harness.Governance.Verify(governance => governance.RecordAccessAsync("interaction-1", It.IsAny<ContactCenterActor>(), "call-recordings-share", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EraseCallRecording_IsRefusedUnderLegalHold()
    {
        // Arrange
        var harness = new ContactCenterOperationsToolHarness();
        var recording = AddRecording(harness, "rec-1", CallerId, "interaction-1");
        harness.Governance.Setup(governance => governance.EraseAsync("interaction-1", It.IsAny<ContactCenterActor>(), "Privacy request", It.IsAny<CancellationToken>()))
            .ReturnsAsync(RecordingErasureDecision.Deny(ContactCenterConstants.RecordingErasureDenyReason.LegalHold));

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new EraseCallRecordingTool(), harness.Build(), new() { ["id"] = "rec-1", ["reason"] = "Privacy request" });

        // Assert
        Assert.Equal("Nothing was erased: the recording is under legal hold and cannot be erased.", result);
        Assert.Null(recording.ErasedUtc);
        harness.RecordingMedia.Verify(store => store.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EraseCallRecording_ErasesThroughGovernance()
    {
        // Arrange
        var harness = new ContactCenterOperationsToolHarness();
        var recording = AddRecording(harness, "rec-1", CallerId, "interaction-1");
        harness.Governance.Setup(governance => governance.EraseAsync("interaction-1", It.IsAny<ContactCenterActor>(), "Privacy request", It.IsAny<CancellationToken>()))
            .ReturnsAsync(RecordingErasureDecision.Erase());

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new EraseCallRecordingTool(), harness.Build(), new() { ["id"] = "rec-1", ["reason"] = "Privacy request" });

        // Assert
        Assert.True(AgentToolTestContext.ReadJson(result).GetProperty("erased").GetBoolean());
        Assert.Equal(ContactCenterOperationsToolHarness.Now, recording.ErasedUtc);
        harness.Recordings.Verify(store => store.UpdateAsync(recording, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListSharedVoicemails_ListsTheEntitledQueuesVoicemailsWithMaskedNumbers()
    {
        // Arrange
        var harness = new ContactCenterOperationsToolHarness();
        var access = new SharedVoicemailAccess { UserId = CallerId, CanAccess = true, QueueIds = ["queue-1"] };
        harness.VoicemailAccess.Setup(service => service.GetAccessAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>(), It.IsAny<CancellationToken>())).ReturnsAsync(access);
        harness.Queues.Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new ActivityQueue { ItemId = "queue-1", Name = "Support" },
            new ActivityQueue { ItemId = "queue-2", Name = "Billing" },
        ]);
        SharedVoicemailQuery query = null;
        harness.Voicemails.Setup(service => service.ListAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>(), It.IsAny<SharedVoicemailQuery>(), It.IsAny<CancellationToken>()))
            .Callback((System.Security.Claims.ClaimsPrincipal _, SharedVoicemailQuery value, CancellationToken _) => query = value)
            .ReturnsAsync(new SharedVoicemailPage
            {
                Count = 1,
                Entries = [new SharedVoicemail { ItemId = "vm-1", QueueId = "queue-1", CallerNumber = "+15550104455", Status = SharedVoicemailStatus.New, ReceivedUtc = ContactCenterOperationsToolHarness.Now }],
            });

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListSharedVoicemailsTool(), harness.Build(), new() { ["queue"] = "Support", ["status"] = "New" });

        // Assert
        Assert.Equal(new[] { "queue-1" }, query.QueueIds);
        Assert.Equal(SharedVoicemailStatus.New, query.Status);
        var item = Assert.Single(AgentToolTestContext.ReadJson(result).GetProperty("items").EnumerateArray());
        Assert.Equal("4455", item.GetProperty("callerNumberEnding").GetString());
        Assert.True(item.GetProperty("youCan").GetProperty("claim").GetBoolean());
        Assert.DoesNotContain("+15550104455", result);
    }

    [Fact]
    public async Task ListSharedVoicemails_RefusesAQueueTheCallerMayNotSee()
    {
        // Arrange
        var harness = new ContactCenterOperationsToolHarness();
        harness.VoicemailAccess.Setup(service => service.GetAccessAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SharedVoicemailAccess { UserId = CallerId, CanAccess = true, QueueIds = ["queue-1"] });
        harness.Queues.Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new ActivityQueue { ItemId = "queue-2", Name = "Billing" }]);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListSharedVoicemailsTool(), harness.Build(), new() { ["queue"] = "Billing" });

        // Assert
        Assert.StartsWith("No queue you may see voicemail of", result);
        Assert.Empty(harness.Voicemails.Invocations);
    }

    [Fact]
    public async Task UpdateSharedVoicemail_RelaysTheServicesRefusal()
    {
        // Arrange
        var harness = new ContactCenterOperationsToolHarness();
        harness.Voicemails.Setup(service => service.ClaimAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>(), "vm-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(SharedVoicemailActionResult.Failure(SharedVoicemailActionStatus.Conflict, new SharedVoicemail { ClaimedByUserName = "omar" }, SharedVoicemailReasons.ClaimedByAnotherUser));

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new UpdateSharedVoicemailTool(), harness.Build(), new() { ["id"] = "vm-1", ["action"] = "claim" });

        // Assert
        Assert.Equal("Nothing was done: omar is already handling this voicemail.", result);
    }

    [Fact]
    public async Task UpdateSharedVoicemail_ResolvesWithTheNote()
    {
        // Arrange
        var harness = new ContactCenterOperationsToolHarness();
        harness.Voicemails.Setup(service => service.ResolveAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>(), "vm-1", "Issue solved", It.IsAny<CancellationToken>()))
            .ReturnsAsync(SharedVoicemailActionResult.Success(new SharedVoicemail { ItemId = "vm-1", Status = SharedVoicemailStatus.Resolved }));

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new UpdateSharedVoicemailTool(), harness.Build(), new() { ["id"] = "vm-1", ["action"] = "resolve", ["note"] = "Issue solved" });

        // Assert
        Assert.Equal("Resolved", AgentToolTestContext.ReadJson(result).GetProperty("status").GetString());
    }

    [Fact]
    public async Task CallBackSharedVoicemail_AsksTheCallersSoftPhoneToDial()
    {
        // Arrange
        var harness = new ContactCenterOperationsToolHarness();
        harness.Voicemails.Setup(service => service.RequestCallbackAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>(), "vm-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(SharedVoicemailActionResult.Success(new SharedVoicemail { ItemId = "vm-1", CallerNumber = "+15550104455" }));

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new CallBackSharedVoicemailTool(), harness.Build(), new() { ["id"] = "vm-1" });

        // Assert
        Assert.True(AgentToolTestContext.ReadJson(result).GetProperty("requested").GetBoolean());
        harness.SoftPhones.Verify(notifier => notifier.DialRequestedAsync(CallerId, It.Is<TelephonyDialRequest>(request => request.Number == "+15550104455")), Times.Once);
    }

    [Fact]
    public async Task DeleteSharedVoicemail_IsRefusedUnderLegalHold()
    {
        // Arrange
        var harness = new ContactCenterOperationsToolHarness();
        harness.Voicemails.Setup(service => service.DeleteAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>(), "vm-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(SharedVoicemailActionResult.Failure(SharedVoicemailActionStatus.Conflict, reasonCode: SharedVoicemailReasons.LegalHold));

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteSharedVoicemailTool(), harness.Build(), new() { ["id"] = "vm-1" });

        // Assert
        Assert.Equal("Nothing was done: the voicemail's recording is under legal hold and cannot be deleted.", result);
    }

    [Fact]
    public async Task DeleteSharedVoicemail_DeletesThroughTheService()
    {
        // Arrange
        var harness = new ContactCenterOperationsToolHarness();
        harness.Voicemails.Setup(service => service.DeleteAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>(), "vm-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(SharedVoicemailActionResult.Success(new SharedVoicemail { ItemId = "vm-1" }));

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteSharedVoicemailTool(), harness.Build(), new() { ["id"] = "vm-1" });

        // Assert
        Assert.True(AgentToolTestContext.ReadJson(result).GetProperty("deleted").GetBoolean());
    }

    [Fact]
    public async Task CallBackSharedVoicemail_DoesNotDialWhenTheServiceRefuses()
    {
        // Arrange
        var harness = new ContactCenterOperationsToolHarness();
        harness.Voicemails.Setup(service => service.RequestCallbackAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>(), "vm-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(SharedVoicemailActionResult.Failure(SharedVoicemailActionStatus.Conflict, reasonCode: SharedVoicemailReasons.NoCallerNumber));

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new CallBackSharedVoicemailTool(), harness.Build(), new() { ["id"] = "vm-1" });

        // Assert
        Assert.Equal("Nothing was done: the caller left no number to call back.", result);
        harness.SoftPhones.Verify(notifier => notifier.DialRequestedAsync(It.IsAny<string>(), It.IsAny<TelephonyDialRequest>()), Times.Never);
    }

    [Fact]
    public async Task ShareCallRecording_MakesNoLinkForAnUnknownRecording()
    {
        // Arrange
        var harness = new ContactCenterOperationsToolHarness();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ShareCallRecordingTool(), harness.Build(), new() { ["id"] = "missing" });

        // Assert
        Assert.Equal("No link was made: no recording you may hear has the id 'missing'.", result);
        Assert.Empty(harness.Governance.Invocations);
    }

    [Fact]
    public async Task ListCallRecordings_RefusesADateItCannotRead()
    {
        // Arrange
        var harness = new ContactCenterOperationsToolHarness();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListCallRecordingsTool(), harness.Build(), new() { ["from"] = "yesterday" });

        // Assert
        Assert.StartsWith("from must be a date", result);
        Assert.Empty(harness.Recordings.Invocations);
    }

    private static bool OwnRecordingsOnly(Permission permission, object resource)
        => permission.Name != ContactCenterPermissions.ListenToAllCallRecordings.Name;

    private static CallRecording AddRecording(ContactCenterOperationsToolHarness harness, string id, string agentUserId, string interactionId)
    {
        var recording = new CallRecording
        {
            ItemId = id,
            AgentUserId = agentUserId,
            InteractionId = interactionId,
            StorageReference = $"store/{id}",
            StoredUtc = ContactCenterOperationsToolHarness.Now.AddHours(-1),
            StartedUtc = ContactCenterOperationsToolHarness.Now.AddHours(-1),
            CustomerAddress = "+15550104455",
        };

        harness.Recordings.Setup(store => store.FindByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(recording);

        return recording;
    }
}
