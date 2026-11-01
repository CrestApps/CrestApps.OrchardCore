using CrestApps.Core;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Realtime;
using CrestApps.Core.AI.Speech;
using CrestApps.OrchardCore.AI.Chat.Services;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Chat;

/// <summary>
/// Which voices a profile's speech-to-speech deployment does not offer, so the editor does not save one.
/// </summary>
/// <remarks>
/// Live, a profile moved from a cascaded deployment to OpenAI's realtime model kept its ElevenLabs voice. OpenAI
/// refused the session's set-up over it, and the call ran with neither the profile's instructions nor its tools.
/// </remarks>
public sealed class RealtimeVoiceOfferTests
{
    private const string ElevenLabsVoice = "EXAVITQu4vr4xnSDxMaL";

    [Fact]
    public async Task AVoiceOfAnotherProvider_IsNotOffered()
    {
        var deployment = Realtime("GptRealtime");

        var withoutVoice = await RealtimeVoiceOffer.FindDeploymentWithoutVoiceAsync(
            Resolving("GptRealtime", deployment),
            Listing(deployment, "alloy", "marin"),
            "GptRealtime",
            ElevenLabsVoice);

        Assert.Same(deployment, withoutVoice);
    }

    [Fact]
    public async Task OneOfTheDeploymentsVoices_IsOffered()
    {
        var deployment = Realtime("GptRealtime");

        var withoutVoice = await RealtimeVoiceOffer.FindDeploymentWithoutVoiceAsync(
            Resolving("GptRealtime", deployment),
            Listing(deployment, "alloy", "marin"),
            "GptRealtime",
            "marin");

        Assert.Null(withoutVoice);
    }

    [Fact]
    public async Task TheSiteDefault_IsCheckedWhenNoDeploymentIsChosen()
    {
        // An empty conversation deployment means the site's default, and a call resolves that deployment.
        var deployment = Realtime("SiteDefault");

        var withoutVoice = await RealtimeVoiceOffer.FindDeploymentWithoutVoiceAsync(
            Resolving(null, deployment),
            Listing(deployment, "alloy"),
            null,
            ElevenLabsVoice);

        Assert.Same(deployment, withoutVoice);
    }

    [Fact]
    public async Task NoVoice_IsNotChecked()
    {
        var voiceResolver = new Mock<IRealtimeVoiceResolver>(MockBehavior.Strict);

        var withoutVoice = await RealtimeVoiceOffer.FindDeploymentWithoutVoiceAsync(
            Mock.Of<IAIDeploymentManager>(),
            voiceResolver.Object,
            "GptRealtime",
            null);

        Assert.Null(withoutVoice);
    }

    [Fact]
    public async Task ACascadedDeployment_IsNotChecked()
    {
        // It speaks through its text-to-speech deployment, whose voices it was given.
        var deployment = Realtime("VoiceNoOpenAI");
        deployment.Put(new CascadedRealtimeMetadata
        {
            SpeechToTextDeploymentName = "stt",
            ChatDeploymentName = "chat",
            TextToSpeechDeploymentName = "tts",
        });

        var withoutVoice = await RealtimeVoiceOffer.FindDeploymentWithoutVoiceAsync(
            Resolving("VoiceNoOpenAI", deployment),
            Listing(deployment, "alloy"),
            "VoiceNoOpenAI",
            ElevenLabsVoice);

        Assert.Null(withoutVoice);
    }

    [Fact]
    public async Task AProviderThatListsNoVoices_IsNotChecked()
    {
        var deployment = Realtime("Other");

        var withoutVoice = await RealtimeVoiceOffer.FindDeploymentWithoutVoiceAsync(
            Resolving("Other", deployment),
            Listing(deployment),
            "Other",
            ElevenLabsVoice);

        Assert.Null(withoutVoice);
    }

    [Fact]
    public async Task NoRealtimeDeployment_IsNotChecked()
    {
        // The conversation then runs as the speech-to-text plus text-to-speech cascade.
        var withoutVoice = await RealtimeVoiceOffer.FindDeploymentWithoutVoiceAsync(
            Resolving(null, null),
            Mock.Of<IRealtimeVoiceResolver>(),
            null,
            ElevenLabsVoice);

        Assert.Null(withoutVoice);
    }

    [Fact]
    public async Task AVoiceListThatCannotBeRead_DoesNotRefuseTheVoice()
    {
        var deployment = Realtime("GptRealtime");
        var voiceResolver = new Mock<IRealtimeVoiceResolver>();
        voiceResolver.Setup(resolver => resolver.GetVoicesAsync(deployment))
            .ThrowsAsync(new InvalidOperationException("Unable to find connection 'missing' for provider 'OpenAI'."));

        var withoutVoice = await RealtimeVoiceOffer.FindDeploymentWithoutVoiceAsync(
            Resolving("GptRealtime", deployment),
            voiceResolver.Object,
            "GptRealtime",
            ElevenLabsVoice);

        Assert.Null(withoutVoice);
    }

    private static AIDeployment Realtime(string name)
        => new() { Name = name, ModelName = "gpt-realtime", ClientName = "OpenAI" };

    private static IAIDeploymentManager Resolving(string deploymentName, AIDeployment deployment)
    {
        var deploymentManager = new Mock<IAIDeploymentManager>();
        deploymentManager.Setup(manager => manager.ResolveSlotAsync(
                AIDeploymentSlotNames.Realtime,
                deploymentName,
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(deployment);

        return deploymentManager.Object;
    }

    private static IRealtimeVoiceResolver Listing(AIDeployment deployment, params string[] voiceIds)
    {
        var voiceResolver = new Mock<IRealtimeVoiceResolver>();
        voiceResolver.Setup(resolver => resolver.GetVoicesAsync(deployment))
            .ReturnsAsync(voiceIds.Select(id => new SpeechVoice { Id = id, Name = id }).ToArray());

        return voiceResolver.Object;
    }
}
