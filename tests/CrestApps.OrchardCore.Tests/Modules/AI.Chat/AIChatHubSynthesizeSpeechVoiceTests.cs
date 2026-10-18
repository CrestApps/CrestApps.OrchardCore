using System.Runtime.CompilerServices;
using System.Security.Claims;
using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.AI.Speech;
using CrestApps.OrchardCore.AI.Chat.Hubs;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

#pragma warning disable MEAI001 // Text-to-speech APIs from Microsoft.Extensions.AI are preview and require explicit opt-in at each usage site.

namespace CrestApps.OrchardCore.Tests.Modules.AI.Chat;

/// <summary>
/// Bug: read-aloud sent the profile's voice straight to the site's text-to-speech deployment. When a realtime
/// deployment carries the profile's conversation, the editor lists that model's voices, so the profile's voice is
/// often one such as <c>marin</c> that Azure AI Speech does not have, and every read-aloud failed. The hub now keeps
/// only a voice the text-to-speech deployment offers, and otherwise falls back to the site's default voice.
/// </summary>
public sealed class AIChatHubSynthesizeSpeechVoiceTests
{
    private static readonly SpeechVoice[] _azureSpeechVoices =
    [
        new() { Id = "en-US-JennyNeural", Name = "Jenny", Language = "en-US" },
        new() { Id = "en-US-GuyNeural", Name = "Guy", Language = "en-US" },
    ];

    [Fact]
    public async Task SynthesizeSpeech_ProfileVoiceIsARealtimeVoice_SpeaksWithTheSiteDefaultVoice()
    {
        // Arrange
        var harness = new Harness(profileVoice: "marin", siteDefaultVoice: "en-US-JennyNeural", offeredVoices: _azureSpeechVoices);

        // Act: the page sends the profile's voice, as the chat views do.
        await harness.Hub.SynthesizeSpeech("profile-1", "session-1", "Hello there.", "marin");

        // Assert
        Assert.Equal("en-US-JennyNeural", harness.TextToSpeech.VoiceId);
        harness.Caller.Verify(client => client.ReceiveError(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SynthesizeSpeech_ProfileVoiceIsOfferedByTheDeployment_SpeaksWithTheProfileVoice()
    {
        // Arrange
        var harness = new Harness(profileVoice: "en-US-GuyNeural", siteDefaultVoice: "en-US-JennyNeural", offeredVoices: _azureSpeechVoices);

        // Act
        await harness.Hub.SynthesizeSpeech("profile-1", "session-1", "Hello there.", voiceName: null);

        // Assert
        Assert.Equal("en-US-GuyNeural", harness.TextToSpeech.VoiceId);
    }

    [Fact]
    public async Task SynthesizeSpeech_NoCandidateIsOffered_LetsTheProviderPickItsDefaultVoice()
    {
        // Arrange
        var harness = new Harness(profileVoice: "marin", siteDefaultVoice: "cedar", offeredVoices: _azureSpeechVoices);

        // Act
        await harness.Hub.SynthesizeSpeech("profile-1", "session-1", "Hello there.", "marin");

        // Assert
        Assert.True(harness.TextToSpeech.WasCalled);
        Assert.Null(harness.TextToSpeech.VoiceId);
    }

    // A provider that publishes no voice list, such as OpenAI text-to-speech, cannot be checked, so the voice is sent
    // as it was before.
    [Fact]
    public async Task SynthesizeSpeech_DeploymentPublishesNoVoiceList_SendsTheProfileVoice()
    {
        // Arrange
        var harness = new Harness(profileVoice: "alloy", siteDefaultVoice: null, offeredVoices: []);

        // Act
        await harness.Hub.SynthesizeSpeech("profile-1", "session-1", "Hello there.", "alloy");

        // Assert
        Assert.Equal("alloy", harness.TextToSpeech.VoiceId);
    }

    [Fact]
    public async Task SelectVoiceAsync_AskedTwice_FetchesTheVoiceListOnce()
    {
        // Arrange
        var resolver = new Mock<ISpeechVoiceResolver>();
        resolver
            .Setup(r => r.GetSpeechVoicesAsync(It.IsAny<AIDeployment>()))
            .ReturnsAsync(_azureSpeechVoices);

        var selector = new TextToSpeechVoiceSelector(
            resolver.Object,
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<TextToSpeechVoiceSelector>.Instance);
        var deployment = new AIDeployment { ItemId = "tts-1", Name = "tts", ClientName = "AzureSpeech" };

        // Act
        var first = await selector.SelectVoiceAsync(deployment, "marin", "en-us-jennyneural");
        var second = await selector.SelectVoiceAsync(deployment, "en-US-GuyNeural");

        // Assert
        Assert.Equal("en-us-jennyneural", first);
        Assert.Equal("en-US-GuyNeural", second);
        resolver.Verify(r => r.GetSpeechVoicesAsync(It.IsAny<AIDeployment>()), Times.Once);
    }

    private sealed class Harness
    {
        public Harness(string profileVoice, string siteDefaultVoice, SpeechVoice[] offeredVoices)
        {
            var profile = new AIProfile { ItemId = "profile-1", Name = "support", Type = AIProfileType.Chat };
            profile.AlterSettings<ChatModeProfileSettings>(settings => settings.VoiceName = profileVoice);

            var profileManager = new Mock<IAIProfileManager>();
            profileManager
                .Setup(manager => manager.FindByIdAsync("profile-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(profile);

            var deployment = new AIDeployment { ItemId = "tts-1", Name = "tts", ClientName = "AzureSpeech" };

            var deploymentManager = new Mock<IAIDeploymentManager>();
            deploymentManager
                .Setup(manager => manager.FindByNameAsync("tts", It.IsAny<CancellationToken>()))
                .ReturnsAsync(deployment);

            var clientFactory = new Mock<IAIClientFactory>();
            clientFactory
                .Setup(factory => factory.CreateTextToSpeechClientAsync(deployment))
                .ReturnsAsync(TextToSpeech);

            var voiceResolver = new Mock<ISpeechVoiceResolver>();
            voiceResolver
                .Setup(resolver => resolver.GetSpeechVoicesAsync(deployment))
                .ReturnsAsync(offeredVoices);

            var services = new ServiceCollection()
                .AddSingleton(profileManager.Object)
                .AddSingleton(deploymentManager.Object)
                .AddSingleton(clientFactory.Object)
                .AddSingleton(voiceResolver.Object)
                .AddSingleton<IMemoryCache>(new MemoryCache(new MemoryCacheOptions()))
                .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
                .AddSingleton<TextToSpeechVoiceSelector>()
                .BuildServiceProvider();

            var clients = new Mock<IHubCallerClients<IAIChatHubClient>>();
            clients.SetupGet(c => c.Caller).Returns(Caller.Object);

            var context = new Mock<HubCallerContext>();
            context.SetupGet(c => c.ConnectionId).Returns("connection-1");
            context.SetupGet(c => c.ConnectionAborted).Returns(CancellationToken.None);
            context.SetupGet(c => c.Features).Returns(new FeatureCollection());
            context.SetupGet(c => c.User).Returns(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "Test")));

            var deploymentSettings = new DefaultAIDeploymentSettings
            {
                DefaultTextToSpeechDeploymentName = "tts",
                DefaultTextToSpeechVoiceId = siteDefaultVoice,
            };

            Hub = new TestAIChatHub(services, deploymentSettings)
            {
                Clients = clients.Object,
                Context = context.Object,
            };
        }

        public TestAIChatHub Hub { get; }

        public Mock<IAIChatHubClient> Caller { get; } = new();

        public RecordingTextToSpeechClient TextToSpeech { get; } = new();
    }

    // The production hub runs each invocation in an Orchard shell child scope, authorizes the profile through the
    // access evaluator and reads the deployment settings from the site; the test supplies all three directly, so what
    // is exercised is which voice reaches the text-to-speech client.
    private sealed class TestAIChatHub : AIChatHub
    {
        private readonly IServiceProvider _services;
        private readonly DefaultAIDeploymentSettings _deploymentSettings;

        public TestAIChatHub(IServiceProvider services, DefaultAIDeploymentSettings deploymentSettings)
            : base(services, TimeProvider.System, NullLogger<AIChatHub>.Instance, new PassThroughStringLocalizer<AIChatHub>())
        {
            _services = services;
            _deploymentSettings = deploymentSettings;
        }

        protected override Task ExecuteInScopeAsync(Func<IServiceProvider, Task> action)
            => action(_services);

        protected override Task<bool> AuthorizeProfileAsync(IServiceProvider services, AIProfile profile)
            => Task.FromResult(true);

        protected override Task<DefaultAIDeploymentSettings> GetDeploymentSettingsAsync(IServiceProvider services)
            => Task.FromResult(_deploymentSettings);
    }

    private sealed class RecordingTextToSpeechClient : ITextToSpeechClient
    {
        public bool WasCalled { get; private set; }

        public string VoiceId { get; private set; }

        public Task<TextToSpeechResponse> GetAudioAsync(string text, TextToSpeechOptions options = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public async IAsyncEnumerable<TextToSpeechResponseUpdate> GetStreamingAudioAsync(
            string text,
            TextToSpeechOptions options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            VoiceId = options?.VoiceId;

            await Task.CompletedTask;

            yield break;
        }

        public object GetService(Type serviceType, object serviceKey = null)
            => null;

        public void Dispose()
        {
        }
    }
}
