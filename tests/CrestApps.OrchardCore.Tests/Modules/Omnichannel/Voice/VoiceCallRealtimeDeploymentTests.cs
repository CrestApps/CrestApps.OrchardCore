using CrestApps.Core.AI.Capabilities;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// Which realtime deployment holds a call: the one the chat would use for the same profile.
/// </summary>
/// <remarks>
/// A profile whose conversation deployment is empty says, in its editor, "Use the site's default realtime
/// deployment". Live, such a profile ran every call turn-based instead: the call asked for the profile's chat
/// deployment, which has been a text model since the two were split, and a text model cannot hold a session.
/// </remarks>
public sealed class VoiceCallRealtimeDeploymentTests
{
    private static readonly AIDeployment _realtime = new() { ItemId = "realtime-1", Name = "GptRealtime" };
    private static readonly AIDeployment _siteDefault = new() { ItemId = "realtime-2", Name = "SiteDefaultRealtime" };

    [Fact]
    public async Task ANamedConversationDeployment_HoldsTheCall()
    {
        // Arrange
        var services = new Services();
        services.Realtime("GptRealtime", _realtime);

        // Act
        var deployment = await services.ResolveAsync("GptRealtime", "Gpt4oStructured");

        // Assert
        Assert.Same(_realtime, deployment);
        services.SlotNotAsked();
    }

    [Fact]
    public async Task ANamedConversationDeploymentThatCannotHoldASession_IsNotSwappedForAnother()
    {
        // Arrange
        // The profile chose this model. Holding the call on the site's model instead would sound fine on a model
        // nobody chose, so the call takes turns and the choice is reported.
        var services = new Services();
        services.Slot(_siteDefault);

        // Act
        var deployment = await services.ResolveAsync("Gpt4oStructured", chatDeploymentName: null);

        // Assert
        Assert.Null(deployment);
        services.SlotNotAsked();
    }

    [Fact]
    public async Task WithNoConversationDeployment_TheSitesRealtimeDeploymentHoldsTheCall()
    {
        // Arrange
        // Every profile saved since the split: the chat deployment is a text model, so it is not the answer.
        var services = new Services();
        services.Slot(_siteDefault);

        // Act
        var deployment = await services.ResolveAsync(conversationDeploymentName: null, "Gpt4oStructured");

        // Assert
        Assert.Same(_siteDefault, deployment);
    }

    [Fact]
    public async Task AProfileSavedBeforeTheSplit_KeepsItsRealtimeChatDeployment()
    {
        // Arrange
        // Before the conversation deployment existed, the speech-to-speech model was the chat deployment.
        var services = new Services();
        services.Realtime("GptRealtime", _realtime);
        services.Slot(_siteDefault);

        // Act
        var deployment = await services.ResolveAsync(conversationDeploymentName: "  ", "GptRealtime");

        // Assert
        Assert.Same(_realtime, deployment);
        services.SlotNotAsked();
    }

    [Fact]
    public async Task WithNoRealtimeDeploymentAnywhere_TheCallTakesTurns()
    {
        // Arrange
        var services = new Services();

        // Act
        var deployment = await services.ResolveAsync(conversationDeploymentName: null, "Gpt4oStructured");

        // Assert
        Assert.Null(deployment);
    }

    [Fact]
    public async Task WithNoDeploymentsNamedAtAll_TheSitesRealtimeDeploymentHoldsTheCall()
    {
        // Arrange
        var services = new Services();
        services.Slot(_siteDefault);

        // Act
        var deployment = await services.ResolveAsync(conversationDeploymentName: null, chatDeploymentName: null);

        // Assert
        Assert.Same(_siteDefault, deployment);
    }

    private sealed class Services
    {
        private readonly Mock<IAIDeploymentCapabilityService> _capabilities = new();
        private readonly Mock<IAIDeploymentManager> _deployments = new();

        public void Realtime(string name, AIDeployment deployment)
            => _capabilities
                .Setup(x => x.ResolveDeploymentWithFeatureAsync(AIDeploymentFeatureNames.Realtime, name, It.IsAny<CancellationToken>()))
                .ReturnsAsync(deployment);

        public void Slot(AIDeployment deployment)
            => _deployments
                .Setup(x => x.ResolveSlotAsync(
                    AIDeploymentSlotNames.Realtime,
                    null,
                    It.IsAny<string>(),
                    It.IsAny<IReadOnlyDictionary<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(deployment);

        public void SlotNotAsked()
            => _deployments.Verify(
                x => x.ResolveSlotAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<IReadOnlyDictionary<string, string>>(),
                    It.IsAny<CancellationToken>()),
                Times.Never());

        public async Task<AIDeployment> ResolveAsync(string conversationDeploymentName, string chatDeploymentName)
            => await VoiceAgentConversationLoop.ResolveRealtimeDeploymentAsync(
                conversationDeploymentName,
                chatDeploymentName,
                _capabilities.Object,
                _deployments.Object,
                TestContext.Current.CancellationToken);
    }
}
