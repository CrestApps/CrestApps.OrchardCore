using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// The bookkeeping that runs after a call or a save commits. Every task reloads its record and acts on what is stored
/// now, not on what the call saw: a copy is recorded only against the master it was made from, a replaced greeting's
/// copies are deleted only once the save landed, and a failure is logged rather than thrown.
/// </summary>
public sealed class DeferredVoiceMediaCopyRecorderTests
{
    [Fact]
    public async Task RecordLibraryCopyAsync_WhenTheMasterIsStillTheOneCopied_RecordsTheCopyAndStoresTheEntry()
    {
        // Arrange
        var item = new VoiceMediaItem
        {
            ItemId = "media-1",
            MasterStorageReference = "voice-media-abc",
            ProviderName = "Telnyx",
            MediaReference = "telnyx-copy",
            ProviderReferences = new Dictionary<string, string> { ["Telnyx"] = "telnyx-copy" },
        };
        var store = CreateStore(item);
        var provisioning = new Mock<IVoiceMediaProvisioningService>(MockBehavior.Strict);

        // Act
        await DeferredVoiceMediaCopyRecorder.RecordLibraryCopyAsync(CreateServices(store: store, provisioning: provisioning), "media-1", "voice-media-abc", "OtherProvider", "other-copy", null);

        // Assert
        Assert.Equal("other-copy", item.ProviderReferences["OtherProvider"]);
        Assert.Equal("telnyx-copy", item.ProviderReferences["Telnyx"]);
        Assert.Equal("OtherProvider", item.ProviderName);
        Assert.Equal("other-copy", item.MediaReference);
        store.Verify(value => value.UpdateAsync(item, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecordLibraryCopyAsync_WhenAnOperatorReplacedTheAudioMeanwhile_DeletesTheCopyInsteadOfRecordingIt()
    {
        // Arrange
        // The call copied the old audio; an operator uploaded new audio and the old master was deleted. Recording the
        // copy would point the new entry at old audio, and restore nothing. The copy is removed from the provider.
        var item = new VoiceMediaItem
        {
            ItemId = "media-1",
            Name = "Renamed meanwhile",
            MasterStorageReference = "voice-media-new",
        };
        var store = CreateStore(item);
        var provisioning = new Mock<IVoiceMediaProvisioningService>();
        IVoiceMediaAsset deleted = null;
        provisioning
            .Setup(service => service.DeleteEverywhereAsync(It.IsAny<IVoiceMediaAsset>(), It.IsAny<CancellationToken>()))
            .Callback((IVoiceMediaAsset media, CancellationToken _) => deleted = media)
            .Returns(Task.CompletedTask);

        // Act
        await DeferredVoiceMediaCopyRecorder.RecordLibraryCopyAsync(CreateServices(store: store, provisioning: provisioning), "media-1", "voice-media-old", "OtherProvider", "other-copy", null);

        // Assert
        Assert.Empty(item.ProviderReferences);
        Assert.Equal("Renamed meanwhile", item.Name);
        Assert.Equal("voice-media-new", item.MasterStorageReference);
        store.Verify(value => value.UpdateAsync(It.IsAny<VoiceMediaItem>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.NotNull(deleted);
        Assert.Null(deleted.MasterStorageReference);
        Assert.Equal("other-copy", Assert.Single(deleted.ProviderReferences).Value);
    }

    [Fact]
    public async Task RecordLibraryCopyAsync_WhenAnotherCopyWasRecordedMeanwhile_KeepsItAndDeletesTheDuplicate()
    {
        // Arrange
        // A re-sync, or another node's call, recorded a copy for this provider while this one was in flight. The
        // recorded copy is the one callers play; the copy made here would only be a second, unreferenced upload.
        var item = new VoiceMediaItem
        {
            ItemId = "media-1",
            MasterStorageReference = "voice-media-abc",
            ProviderReferences = new Dictionary<string, string> { ["OtherProvider"] = "other-recorded" },
        };
        var store = CreateStore(item);
        var provisioning = new Mock<IVoiceMediaProvisioningService>();
        IVoiceMediaAsset deleted = null;
        provisioning
            .Setup(service => service.DeleteEverywhereAsync(It.IsAny<IVoiceMediaAsset>(), It.IsAny<CancellationToken>()))
            .Callback((IVoiceMediaAsset media, CancellationToken _) => deleted = media)
            .Returns(Task.CompletedTask);
        var uploads = new Mock<IVoiceMediaUploadCoordinator>();

        // Act
        await DeferredVoiceMediaCopyRecorder.RecordLibraryCopyAsync(CreateServices(store: store, provisioning: provisioning, uploads: uploads), "media-1", "voice-media-abc", "OtherProvider", "other-duplicate", null);

        // Assert
        Assert.Equal("other-recorded", Assert.Single(item.ProviderReferences).Value);
        store.Verify(value => value.UpdateAsync(It.IsAny<VoiceMediaItem>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.NotNull(deleted);
        Assert.Equal("other-duplicate", Assert.Single(deleted.ProviderReferences).Value);
        uploads.Verify(coordinator => coordinator.Forget("OtherProvider", "voice-media-abc"), Times.Once);
    }

    [Fact]
    public async Task RecordLibraryCopyAsync_ReplacesTheCopyTheProviderReportedMissing()
    {
        // Arrange
        // The provider lost "other-old", the call made "other-new" to replace it. The old reference is still what
        // the entry records, and it is the one being replaced, so the new copy takes its place.
        var item = new VoiceMediaItem
        {
            ItemId = "media-1",
            MasterStorageReference = "voice-media-abc",
            ProviderReferences = new Dictionary<string, string> { ["OtherProvider"] = "other-old" },
        };
        var store = CreateStore(item);
        var provisioning = new Mock<IVoiceMediaProvisioningService>(MockBehavior.Strict);

        // Act
        await DeferredVoiceMediaCopyRecorder.RecordLibraryCopyAsync(CreateServices(store: store, provisioning: provisioning), "media-1", "voice-media-abc", "OtherProvider", "other-new", "other-old");

        // Assert
        Assert.Equal("other-new", Assert.Single(item.ProviderReferences).Value);
        store.Verify(value => value.UpdateAsync(item, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecordLibraryCopyAsync_WhenTheEntryIsGone_DeletesTheCopy()
    {
        // Arrange
        var store = CreateStore(item: null);
        var provisioning = new Mock<IVoiceMediaProvisioningService>();
        provisioning
            .Setup(service => service.DeleteEverywhereAsync(It.IsAny<IVoiceMediaAsset>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await DeferredVoiceMediaCopyRecorder.RecordLibraryCopyAsync(CreateServices(store: store, provisioning: provisioning), "media-1", "voice-media-abc", "OtherProvider", "other-copy", null);

        // Assert
        provisioning.Verify(
            service => service.DeleteEverywhereAsync(It.Is<IVoiceMediaAsset>(media => media.ProviderReferences["OtherProvider"] == "other-copy"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RecordGreetingCopyAsync_WhenTheMasterIsStillTheOneCopied_RecordsTheCopyAndSaves()
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
        var provisioning = new Mock<IVoiceMediaProvisioningService>(MockBehavior.Strict);

        // Act
        await DeferredVoiceMediaCopyRecorder.RecordGreetingCopyAsync(CreateServices(agents: agents, provisioning: provisioning), "agent-9", "voice-media-greeting", "OtherProvider", "cc-vm-greeting-other", null);

        // Assert
        Assert.Equal("cc-vm-greeting-other", agent.VoicemailGreetingMedia.ProviderReferences["OtherProvider"]);
        Assert.Equal("cc-vm-greeting-telnyx", agent.VoicemailGreetingMedia.ProviderReferences["Telnyx"]);
        agents.Verify(manager => manager.UpdateAsync(agent, It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecordGreetingCopyAsync_WhenTheCopyIsAlreadyRecorded_DoesNotSave()
    {
        // Arrange
        // Two calls reached the voicemail before the first bookkeeping ran; the second finds nothing to do.
        var agent = new AgentProfile
        {
            ItemId = "agent-9",
            VoicemailGreetingMedia = new VoiceMediaAsset
            {
                MasterStorageReference = "voice-media-greeting",
                ProviderReferences = new Dictionary<string, string> { ["otherprovider"] = "cc-vm-greeting-other" },
            },
        };
        var agents = CreateAgents(agent);

        // Act
        await DeferredVoiceMediaCopyRecorder.RecordGreetingCopyAsync(CreateServices(agents: agents), "agent-9", "voice-media-greeting", "OtherProvider", "cc-vm-greeting-other", null);

        // Assert
        agents.Verify(manager => manager.UpdateAsync(It.IsAny<AgentProfile>(), It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RecordGreetingCopyAsync_WhenAnotherCopyWasRecordedMeanwhile_KeepsItAndDeletesTheDuplicate()
    {
        // Arrange
        var agent = new AgentProfile
        {
            ItemId = "agent-9",
            VoicemailGreetingMedia = new VoiceMediaAsset
            {
                MasterStorageReference = "voice-media-greeting",
                ProviderReferences = new Dictionary<string, string> { ["OtherProvider"] = "cc-vm-greeting-recorded" },
            },
        };
        var agents = CreateAgents(agent);
        var provisioning = new Mock<IVoiceMediaProvisioningService>();
        IVoiceMediaAsset deleted = null;
        provisioning
            .Setup(service => service.DeleteEverywhereAsync(It.IsAny<IVoiceMediaAsset>(), It.IsAny<CancellationToken>()))
            .Callback((IVoiceMediaAsset media, CancellationToken _) => deleted = media)
            .Returns(Task.CompletedTask);

        // Act
        await DeferredVoiceMediaCopyRecorder.RecordGreetingCopyAsync(CreateServices(agents: agents, provisioning: provisioning), "agent-9", "voice-media-greeting", "OtherProvider", "cc-vm-greeting-duplicate", null);

        // Assert
        Assert.Equal("cc-vm-greeting-recorded", Assert.Single(agent.VoicemailGreetingMedia.ProviderReferences).Value);
        agents.Verify(manager => manager.UpdateAsync(It.IsAny<AgentProfile>(), It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.NotNull(deleted);
        Assert.Equal("cc-vm-greeting-duplicate", Assert.Single(deleted.ProviderReferences).Value);
    }

    [Fact]
    public async Task RecordGreetingCopyAsync_WhenTheAgentReplacedTheGreetingMeanwhile_DeletesTheCopyInsteadOfRecordingIt()
    {
        // Arrange
        // The copy plays the greeting the agent just replaced. Recorded against the new greeting, callers would hear
        // the old one on this provider.
        var agent = new AgentProfile
        {
            ItemId = "agent-9",
            VoicemailGreetingMedia = new VoiceMediaAsset { MasterStorageReference = "voice-media-new" },
        };
        var agents = CreateAgents(agent);
        var provisioning = new Mock<IVoiceMediaProvisioningService>();
        IVoiceMediaAsset deleted = null;
        provisioning
            .Setup(service => service.DeleteEverywhereAsync(It.IsAny<IVoiceMediaAsset>(), It.IsAny<CancellationToken>()))
            .Callback((IVoiceMediaAsset media, CancellationToken _) => deleted = media)
            .Returns(Task.CompletedTask);

        // Act
        await DeferredVoiceMediaCopyRecorder.RecordGreetingCopyAsync(CreateServices(agents: agents, provisioning: provisioning), "agent-9", "voice-media-old", "OtherProvider", "cc-vm-greeting-other", null);

        // Assert
        Assert.Empty(agent.VoicemailGreetingMedia.ProviderReferences);
        agents.Verify(manager => manager.UpdateAsync(It.IsAny<AgentProfile>(), It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.NotNull(deleted);
        Assert.Equal("cc-vm-greeting-other", Assert.Single(deleted.ProviderReferences).Value);
    }

    [Fact]
    public async Task RecordGreetingCopyAsync_WhenTheGreetingWasRemovedMeanwhile_DeletesTheCopy()
    {
        // Arrange
        var agent = new AgentProfile { ItemId = "agent-9" };
        var agents = CreateAgents(agent);
        var provisioning = new Mock<IVoiceMediaProvisioningService>();
        provisioning
            .Setup(service => service.DeleteEverywhereAsync(It.IsAny<IVoiceMediaAsset>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await DeferredVoiceMediaCopyRecorder.RecordGreetingCopyAsync(CreateServices(agents: agents, provisioning: provisioning), "agent-9", "voice-media-greeting", "OtherProvider", "cc-vm-greeting-other", null);

        // Assert
        Assert.Null(agent.VoicemailGreetingMedia);
        provisioning.Verify(service => service.DeleteEverywhereAsync(It.IsAny<IVoiceMediaAsset>(), It.IsAny<CancellationToken>()), Times.Once);
        agents.Verify(manager => manager.UpdateAsync(It.IsAny<AgentProfile>(), It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RecordGreetingCopyAsync_WhenTheSaveFails_LogsAndDoesNotThrow()
    {
        // Arrange
        var agent = new AgentProfile
        {
            ItemId = "agent-9",
            VoicemailGreetingMedia = new VoiceMediaAsset { MasterStorageReference = "voice-media-greeting" },
        };
        var agents = CreateAgents(agent);
        agents
            .Setup(manager => manager.UpdateAsync(It.IsAny<AgentProfile>(), It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("The document was changed by somebody else."));

        // Act
        var exception = await Record.ExceptionAsync(() => DeferredVoiceMediaCopyRecorder.RecordGreetingCopyAsync(CreateServices(agents: agents), "agent-9", "voice-media-greeting", "OtherProvider", "cc-vm-greeting-other", null));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task ReleaseReplacedGreetingCopiesAsync_WhenTheSaveLanded_DeletesTheReplacedCopies()
    {
        // Arrange
        var saved = new VoiceMediaAsset
        {
            MasterStorageReference = "voice-media-new",
            ProviderReferences = new Dictionary<string, string> { ["Telnyx"] = "cc-vm-greeting-new" },
        };
        var replaced = new VoiceMediaAsset
        {
            MasterStorageReference = "voice-media-old",
            ProviderReferences = new Dictionary<string, string> { ["Telnyx"] = "cc-vm-greeting-old" },
        };
        var agent = new AgentProfile { ItemId = "agent-9", VoicemailGreetingMedia = saved };
        var agents = CreateAgents(agent);
        var provisioning = new Mock<IVoiceMediaProvisioningService>();
        provisioning.Setup(service => service.DeleteEverywhereAsync(It.IsAny<IVoiceMediaAsset>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        await DeferredVoiceMediaCopyRecorder.ReleaseReplacedGreetingCopiesAsync(CreateServices(agents: agents, provisioning: provisioning), "agent-9", saved, replaced);

        // Assert
        provisioning.Verify(service => service.DeleteEverywhereAsync(replaced, It.IsAny<CancellationToken>()), Times.Once);
        provisioning.Verify(service => service.DeleteEverywhereAsync(It.IsAny<IVoiceMediaAsset>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReleaseReplacedGreetingCopiesAsync_WhenTheSaveDidNotLand_DeletesTheNewCopiesAndKeepsTheOld()
    {
        // Arrange
        // The commit failed against a concurrent presence change: the profile still references the old greeting,
        // whose master and provider copies must therefore survive; the copies made for the lost save are removed.
        var saved = new VoiceMediaAsset
        {
            MasterStorageReference = "voice-media-new",
            ProviderReferences = new Dictionary<string, string> { ["Telnyx"] = "cc-vm-greeting-new" },
        };
        var replaced = new VoiceMediaAsset
        {
            MasterStorageReference = "voice-media-old",
            ProviderReferences = new Dictionary<string, string> { ["Telnyx"] = "cc-vm-greeting-old" },
        };
        var agent = new AgentProfile
        {
            ItemId = "agent-9",
            VoicemailGreetingMedia = new VoiceMediaAsset
            {
                MasterStorageReference = "voice-media-old",
                ProviderReferences = new Dictionary<string, string> { ["Telnyx"] = "cc-vm-greeting-old" },
            },
        };
        var agents = CreateAgents(agent);
        var provisioning = new Mock<IVoiceMediaProvisioningService>();
        provisioning.Setup(service => service.DeleteEverywhereAsync(It.IsAny<IVoiceMediaAsset>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        await DeferredVoiceMediaCopyRecorder.ReleaseReplacedGreetingCopiesAsync(CreateServices(agents: agents, provisioning: provisioning), "agent-9", saved, replaced);

        // Assert
        provisioning.Verify(service => service.DeleteEverywhereAsync(saved, It.IsAny<CancellationToken>()), Times.Once);
        provisioning.Verify(service => service.DeleteEverywhereAsync(It.IsAny<IVoiceMediaAsset>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReleaseReplacedGreetingCopiesAsync_WhenTheRemovalLanded_DeletesTheRemovedCopies()
    {
        // Arrange
        var removed = new VoiceMediaAsset
        {
            MasterStorageReference = "voice-media-old",
            ProviderReferences = new Dictionary<string, string> { ["Telnyx"] = "cc-vm-greeting-old" },
        };
        var agents = CreateAgents(new AgentProfile { ItemId = "agent-9" });
        var provisioning = new Mock<IVoiceMediaProvisioningService>();
        provisioning.Setup(service => service.DeleteEverywhereAsync(It.IsAny<IVoiceMediaAsset>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        await DeferredVoiceMediaCopyRecorder.ReleaseReplacedGreetingCopiesAsync(CreateServices(agents: agents, provisioning: provisioning), "agent-9", saved: null, removed);

        // Assert
        provisioning.Verify(service => service.DeleteEverywhereAsync(removed, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReleaseReplacedGreetingCopiesAsync_WhenTheRemovalDidNotLand_DeletesNothing()
    {
        // Arrange
        var removed = new VoiceMediaAsset { MasterStorageReference = "voice-media-old" };
        var agents = CreateAgents(new AgentProfile
        {
            ItemId = "agent-9",
            VoicemailGreetingMedia = new VoiceMediaAsset { MasterStorageReference = "voice-media-old" },
        });
        var provisioning = new Mock<IVoiceMediaProvisioningService>(MockBehavior.Strict);

        // Act
        await DeferredVoiceMediaCopyRecorder.ReleaseReplacedGreetingCopiesAsync(CreateServices(agents: agents, provisioning: provisioning), "agent-9", saved: null, removed);

        // Assert
        provisioning.VerifyNoOtherCalls();
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

    private static Mock<IVoiceMediaItemStore> CreateStore(VoiceMediaItem item)
    {
        var store = new Mock<IVoiceMediaItemStore>();
        store.Setup(value => value.FindByIdAsync("media-1", It.IsAny<CancellationToken>())).ReturnsAsync(item);
        store.Setup(value => value.UpdateAsync(It.IsAny<VoiceMediaItem>(), It.IsAny<CancellationToken>())).Returns(ValueTask.CompletedTask);

        return store;
    }

    private static ServiceProvider CreateServices(
        Mock<IAgentProfileManager> agents = null,
        Mock<IVoiceMediaItemStore> store = null,
        Mock<IVoiceMediaProvisioningService> provisioning = null,
        Mock<IVoiceMediaUploadCoordinator> uploads = null)
        => new ServiceCollection()
            .AddLogging()
            .AddSingleton((agents ?? new Mock<IAgentProfileManager>()).Object)
            .AddSingleton((store ?? new Mock<IVoiceMediaItemStore>()).Object)
            .AddSingleton((provisioning ?? new Mock<IVoiceMediaProvisioningService>()).Object)
            .AddSingleton((uploads ?? new Mock<IVoiceMediaUploadCoordinator>()).Object)
            .BuildServiceProvider();
}
