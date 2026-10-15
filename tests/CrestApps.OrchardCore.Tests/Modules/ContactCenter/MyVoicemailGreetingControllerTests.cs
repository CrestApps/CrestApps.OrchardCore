using System.Security.Claims;
using System.Text;
using CrestApps.OrchardCore.ContactCenter.Controllers;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// An agent's recorded greeting follows the same path as a library clip: the platform keeps the master copy, the
/// active provider is given a copy, and the copies the new recording replaces are removed only once the save has
/// committed, because the profile is written constantly by presence changes and a save can still fail at commit.
/// </summary>
public sealed class MyVoicemailGreetingControllerTests
{
    private const string UserId = "user-1";

    [Fact]
    public async Task SaveAudio_StoresTheGreetingThroughTheProvisioningService_AndRecordsBothCopiesOnTheAgent()
    {
        // Arrange
        var agent = new AgentProfile { ItemId = "agent-1", UserId = UserId };
        var harness = CreateHarness(agent);

        IVoiceMediaAsset stored = null;
        string storedPrefix = null;
        harness.Provisioning
            .Setup(service => service.StoreAsync(It.IsAny<IVoiceMediaAsset>(), It.IsAny<Stream>(), "audio/webm", "webm", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((IVoiceMediaAsset media, Stream _, string contentType, string format, string prefix, CancellationToken _) =>
            {
                stored = media;
                storedPrefix = prefix;
                media.MasterStorageReference = "voice-media-new";
                media.ContentType = contentType;
                media.Format = format;
                media.ProviderReferences = new Dictionary<string, string> { ["Telnyx"] = "cc-vm-greeting-new" };

                return Task.FromResult(new VoiceMediaStoreResult
                {
                    MasterStored = true,
                    ProviderName = "Telnyx",
                    ProviderReference = "cc-vm-greeting-new",
                });
            });

        // Act
        var result = await harness.Controller.SaveAudio(CreateAudio("greeting.webm", "audio/webm"));

        // Assert
        Assert.IsType<OkResult>(result);
        Assert.Equal("cc-vm-greeting", storedPrefix);
        Assert.Same(stored, agent.VoicemailGreetingMedia);
        Assert.Equal("voice-media-new", agent.VoicemailGreetingMedia.MasterStorageReference);
        Assert.Equal("cc-vm-greeting-new", agent.VoicemailGreetingMediaName);
        Assert.Null(agent.VoicemailGreetingMediaUrl);
        harness.Agents.Verify(manager => manager.UpdateAsync(agent, It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveAudio_DeletesNothingItself_AndLeavesTheReplacedCopiesToTheDeferredBookkeeping()
    {
        // Arrange
        // UpdateAsync only stages the write; it commits at the end of the request and can fail there. Deleting the
        // old copies now would leave a profile that still references them, with the master gone for good.
        var agent = new AgentProfile
        {
            ItemId = "agent-1",
            UserId = UserId,
            VoicemailGreetingMediaName = "cc-vm-greeting-legacy",
            VoicemailGreetingMedia = new VoiceMediaAsset
            {
                MasterStorageReference = "voice-media-old",
                ProviderReferences = new Dictionary<string, string> { ["Telnyx"] = "cc-vm-greeting-legacy" },
            },
        };
        var harness = CreateHarness(agent);

        harness.Provisioning
            .Setup(service => service.StoreAsync(It.IsAny<IVoiceMediaAsset>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((IVoiceMediaAsset media, Stream _, string _, string _, string _, CancellationToken _) =>
            {
                media.MasterStorageReference = "voice-media-new";
                media.ProviderReferences = new Dictionary<string, string> { ["Telnyx"] = "cc-vm-greeting-new" };

                return Task.FromResult(new VoiceMediaStoreResult
                {
                    MasterStored = true,
                    ProviderName = "Telnyx",
                    ProviderReference = "cc-vm-greeting-new",
                });
            });

        VoiceMediaAsset scheduledSaved = null;
        VoiceMediaAsset scheduledReplaced = null;
        harness.CopyRecorder
            .Setup(recorder => recorder.ReleaseReplacedGreetingCopies("agent-1", It.IsAny<VoiceMediaAsset>(), It.IsAny<VoiceMediaAsset>()))
            .Callback((string _, VoiceMediaAsset saved, VoiceMediaAsset replaced) =>
            {
                scheduledSaved = saved;
                scheduledReplaced = replaced;
            });

        // Act
        await harness.Controller.SaveAudio(CreateAudio("greeting.webm", "audio/webm"));

        // Assert
        harness.Provisioning.Verify(service => service.DeleteEverywhereAsync(It.IsAny<IVoiceMediaAsset>(), It.IsAny<CancellationToken>()), Times.Never);
        harness.CopyRecorder.Verify(recorder => recorder.ReleaseReplacedGreetingCopies("agent-1", It.IsAny<VoiceMediaAsset>(), It.IsAny<VoiceMediaAsset>()), Times.Once);
        Assert.Equal("voice-media-new", scheduledSaved.MasterStorageReference);
        Assert.Equal("cc-vm-greeting-new", scheduledSaved.ProviderReferences["Telnyx"]);
        Assert.Equal("voice-media-old", scheduledReplaced.MasterStorageReference);
        Assert.Equal("cc-vm-greeting-legacy", scheduledReplaced.ProviderReferences["Telnyx"]);
    }

    [Fact]
    public async Task SaveAudio_SchedulesTheBookkeepingEvenWhenNothingWasReplaced_SoALostSaveIsCleanedUp()
    {
        // Arrange
        var agent = new AgentProfile { ItemId = "agent-1", UserId = UserId };
        var harness = CreateHarness(agent);

        harness.Provisioning
            .Setup(service => service.StoreAsync(It.IsAny<IVoiceMediaAsset>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((IVoiceMediaAsset media, Stream _, string _, string _, string _, CancellationToken _) =>
            {
                media.MasterStorageReference = "voice-media-new";

                return Task.FromResult(new VoiceMediaStoreResult { MasterStored = true, ProviderName = "Telnyx", ProviderUploadFailed = true });
            });

        // Act
        await harness.Controller.SaveAudio(CreateAudio("greeting.webm", "audio/webm"));

        // Assert
        harness.CopyRecorder.Verify(
            recorder => recorder.ReleaseReplacedGreetingCopies("agent-1", It.Is<VoiceMediaAsset>(saved => saved.MasterStorageReference == "voice-media-new"), null),
            Times.Once);
    }

    [Fact]
    public async Task SaveAudio_WhenTheAudioIsHeldNowhere_ReportsTheProviderFailure_AndLeavesTheAgentAlone()
    {
        // Arrange
        var agent = new AgentProfile { ItemId = "agent-1", UserId = UserId, VoicemailGreetingMediaName = "cc-vm-greeting-old" };
        var harness = CreateHarness(agent);

        harness.Provisioning
            .Setup(service => service.StoreAsync(It.IsAny<IVoiceMediaAsset>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VoiceMediaStoreResult { ProviderName = "Telnyx", ProviderUploadFailed = true });

        // Act
        var result = await harness.Controller.SaveAudio(CreateAudio("greeting.webm", "audio/webm"));

        // Assert
        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status502BadGateway, status.StatusCode);
        Assert.Equal("cc-vm-greeting-old", agent.VoicemailGreetingMediaName);
        Assert.Null(agent.VoicemailGreetingMedia);
        harness.Agents.Verify(manager => manager.UpdateAsync(It.IsAny<AgentProfile>(), It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()), Times.Never);
        harness.CopyRecorder.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Clear_RemovesTheGreeting_AndLeavesItsCopiesToTheDeferredBookkeeping()
    {
        // Arrange
        var greeting = new VoiceMediaAsset
        {
            MasterStorageReference = "voice-media-greeting",
            ProviderReferences = new Dictionary<string, string> { ["Telnyx"] = "cc-vm-greeting-1" },
        };
        var agent = new AgentProfile
        {
            ItemId = "agent-1",
            UserId = UserId,
            VoicemailGreetingMediaName = "cc-vm-greeting-1",
            VoicemailGreetingMedia = greeting,
        };
        var harness = CreateHarness(agent);

        VoiceMediaAsset scheduledRemoved = null;
        harness.CopyRecorder
            .Setup(recorder => recorder.ReleaseReplacedGreetingCopies("agent-1", null, It.IsAny<VoiceMediaAsset>()))
            .Callback((string _, VoiceMediaAsset _, VoiceMediaAsset removed) => scheduledRemoved = removed);

        // Act
        var result = await harness.Controller.Clear();

        // Assert
        Assert.IsType<RedirectToActionResult>(result);
        Assert.Null(agent.VoicemailGreetingMedia);
        Assert.Null(agent.VoicemailGreetingMediaName);
        harness.Provisioning.Verify(service => service.DeleteEverywhereAsync(It.IsAny<IVoiceMediaAsset>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.NotNull(scheduledRemoved);
        Assert.Equal("voice-media-greeting", scheduledRemoved.MasterStorageReference);
        Assert.Equal("cc-vm-greeting-1", Assert.Single(scheduledRemoved.ProviderReferences).Value);
    }

    [Fact]
    public async Task Clear_WithNoGreeting_SchedulesNothing()
    {
        // Arrange
        var harness = CreateHarness(new AgentProfile { ItemId = "agent-1", UserId = UserId });

        // Act
        await harness.Controller.Clear();

        // Assert
        harness.CopyRecorder.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Index_FlagsAGreetingThatExistsOnlyAtTheProvider()
    {
        // Arrange
        var agent = new AgentProfile { ItemId = "agent-1", UserId = UserId, VoicemailGreetingMediaName = "cc-vm-greeting-legacy" };
        var harness = CreateHarness(agent);

        // Act
        var result = await harness.Controller.Index();

        // Assert
        var model = Assert.IsType<MyVoicemailGreetingViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.True(model.HasAudioGreeting);
        Assert.True(model.IsProviderCopyOnly);
    }

    [Fact]
    public async Task Index_FlagsAGreetingStoredWhileNoMasterCopyCouldBeKept()
    {
        // Arrange
        // The master write failed and only the provider's copy exists: the greeting plays, but it is as exposed as one
        // recorded before master copies were kept, and the agent is told to record it again.
        var agent = new AgentProfile
        {
            ItemId = "agent-1",
            UserId = UserId,
            VoicemailGreetingMedia = new VoiceMediaAsset
            {
                ProviderReferences = new Dictionary<string, string> { ["Telnyx"] = "cc-vm-greeting-1" },
            },
        };
        var harness = CreateHarness(agent);

        // Act
        var result = await harness.Controller.Index();

        // Assert
        var model = Assert.IsType<MyVoicemailGreetingViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.True(model.HasAudioGreeting);
        Assert.True(model.IsProviderCopyOnly);
    }

    [Fact]
    public async Task Index_DoesNotFlagAGreetingWithAMasterCopy()
    {
        // Arrange
        var agent = new AgentProfile
        {
            ItemId = "agent-1",
            UserId = UserId,
            VoicemailGreetingMedia = new VoiceMediaAsset { MasterStorageReference = "voice-media-greeting" },
        };
        var harness = CreateHarness(agent);

        // Act
        var result = await harness.Controller.Index();

        // Assert
        var model = Assert.IsType<MyVoicemailGreetingViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.True(model.HasAudioGreeting);
        Assert.False(model.IsProviderCopyOnly);
    }

    private static FormFile CreateAudio(string fileName, string contentType)
    {
        var bytes = Encoding.UTF8.GetBytes("audio");

        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "audio", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType,
        };
    }

    private static Harness CreateHarness(AgentProfile agent)
    {
        var agents = new Mock<IAgentProfileManager>();
        agents.Setup(manager => manager.FindByUserIdAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(agent);
        agents
            .Setup(manager => manager.UpdateAsync(It.IsAny<AgentProfile>(), It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var provisioning = new Mock<IVoiceMediaProvisioningService>();
        provisioning.SetupGet(service => service.CanUpload).Returns(true);
        provisioning.SetupGet(service => service.ActiveProviderName).Returns("Telnyx");

        var copyRecorder = new Mock<IVoiceMediaCopyRecorder>();

        var controller = new MyVoicemailGreetingController(
            agents.Object,
            provisioning.Object,
            copyRecorder.Object,
            new AllowingAuthorizationService(),
            Mock.Of<IClock>(),
            Mock.Of<INotifier>(),
            new PassThroughHtmlLocalizer<MyVoicemailGreetingController>(),
            new PassThroughStringLocalizer<MyVoicemailGreetingController>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, UserId)], "Test")),
                },
            },
        };

        return new Harness(controller, agents, provisioning, copyRecorder);
    }

    private sealed record Harness(
        MyVoicemailGreetingController Controller,
        Mock<IAgentProfileManager> Agents,
        Mock<IVoiceMediaProvisioningService> Provisioning,
        Mock<IVoiceMediaCopyRecorder> CopyRecorder);

    private sealed class AllowingAuthorizationService : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements)
            => Task.FromResult(AuthorizationResult.Success());

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName)
            => Task.FromResult(AuthorizationResult.Success());
    }
}
