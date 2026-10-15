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
/// active provider is given a copy, and the copies the new recording replaces are removed.
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
        harness.Provisioning.Verify(service => service.DeleteEverywhereAsync(It.IsAny<IVoiceMediaAsset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SaveAudio_RemovesEveryCopyOfTheGreetingItReplaces_IncludingOneFromBeforeMasterCopiesWereKept()
    {
        // Arrange
        var agent = new AgentProfile
        {
            ItemId = "agent-1",
            UserId = UserId,
            VoicemailGreetingMediaName = "cc-vm-greeting-legacy",
        };
        var harness = CreateHarness(agent);

        harness.Provisioning
            .Setup(service => service.StoreAsync(It.IsAny<IVoiceMediaAsset>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VoiceMediaStoreResult
            {
                MasterStored = true,
                ProviderName = "Telnyx",
                ProviderReference = "cc-vm-greeting-new",
            });

        IVoiceMediaAsset deleted = null;
        harness.Provisioning
            .Setup(service => service.DeleteEverywhereAsync(It.IsAny<IVoiceMediaAsset>(), It.IsAny<CancellationToken>()))
            .Callback((IVoiceMediaAsset media, CancellationToken _) => deleted = media)
            .Returns(Task.CompletedTask);

        // Act
        await harness.Controller.SaveAudio(CreateAudio("greeting.webm", "audio/webm"));

        // Assert
        Assert.NotNull(deleted);
        Assert.Equal("cc-vm-greeting-legacy", deleted.ProviderReferences["Telnyx"]);
        Assert.Equal("cc-vm-greeting-new", agent.VoicemailGreetingMediaName);
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
    }

    [Fact]
    public async Task Clear_RemovesTheMasterCopyAndEveryProviderCopy()
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

        IVoiceMediaAsset deleted = null;
        harness.Provisioning
            .Setup(service => service.DeleteEverywhereAsync(It.IsAny<IVoiceMediaAsset>(), It.IsAny<CancellationToken>()))
            .Callback((IVoiceMediaAsset media, CancellationToken _) => deleted = media)
            .Returns(Task.CompletedTask);

        // Act
        var result = await harness.Controller.Clear();

        // Assert
        Assert.IsType<RedirectToActionResult>(result);
        Assert.Null(agent.VoicemailGreetingMedia);
        Assert.Null(agent.VoicemailGreetingMediaName);
        Assert.NotNull(deleted);
        Assert.Equal("voice-media-greeting", deleted.MasterStorageReference);
        Assert.Equal("cc-vm-greeting-1", Assert.Single(deleted.ProviderReferences).Value);
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

        var controller = new MyVoicemailGreetingController(
            agents.Object,
            provisioning.Object,
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

        return new Harness(controller, agents, provisioning);
    }

    private sealed record Harness(
        MyVoicemailGreetingController Controller,
        Mock<IAgentProfileManager> Agents,
        Mock<IVoiceMediaProvisioningService> Provisioning);

    private sealed class AllowingAuthorizationService : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements)
            => Task.FromResult(AuthorizationResult.Success());

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName)
            => Task.FromResult(AuthorizationResult.Success());
    }
}
