using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Capabilities;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Speech;
using CrestApps.OrchardCore.AI.Chat.Drivers;
using CrestApps.OrchardCore.AI.Chat.ViewModels;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Chat;

/// <summary>
/// The voice a profile's editor shows is the voice it saves, whatever the chat mode.
/// </summary>
/// <remarks>
/// Phone calls answered or placed with a profile speak with its voice whatever its chat mode, so the editor shows
/// the voice on every chat profile. Saving a text-only profile still cleared it, so the voice chosen for its calls
/// was dropped and the calls spoke with the model's default.
/// </remarks>
public sealed class ChatModeVoiceSaveTests
{
    [Theory]
    [InlineData(ChatMode.TextInput)]
    [InlineData(ChatMode.AudioInput)]
    [InlineData(ChatMode.Conversation)]
    public async Task AProfile_KeepsItsVoiceWhateverTheChatMode(ChatMode chatMode)
    {
        // Arrange
        var driver = new AIProfileChatModeDisplayDriver(
            Mock.Of<IAIDeploymentManager>(),
            Mock.Of<IAIDeploymentCapabilityService>(),
            null,
            Mock.Of<ISiteService>(),
            Mock.Of<IRealtimeVoiceResolver>(),
            Mock.Of<IStringLocalizer<AIProfileChatModeDisplayDriver>>());
        var profile = new AIProfile { Type = AIProfileType.Chat };

        // Act
        await driver.UpdateAsync(profile, CreateContext(chatMode, " coral "));

        // Assert
        Assert.True(profile.TryGetSettings<ChatModeProfileSettings>(out var settings));
        Assert.Equal("coral", settings.VoiceName);
    }

    [Fact]
    public async Task AProfile_WithNoVoiceChosen_StoresNone()
    {
        // Arrange
        var driver = new AIProfileChatModeDisplayDriver(
            Mock.Of<IAIDeploymentManager>(),
            Mock.Of<IAIDeploymentCapabilityService>(),
            null,
            Mock.Of<ISiteService>(),
            Mock.Of<IRealtimeVoiceResolver>(),
            Mock.Of<IStringLocalizer<AIProfileChatModeDisplayDriver>>());
        var profile = new AIProfile { Type = AIProfileType.Chat };
        profile.AlterSettings<ChatModeProfileSettings>(settings => settings.VoiceName = "coral");

        // Act
        await driver.UpdateAsync(profile, CreateContext(ChatMode.TextInput, "  "));

        // Assert
        Assert.True(profile.TryGetSettings<ChatModeProfileSettings>(out var settings));
        Assert.Null(settings.VoiceName);
    }

    [Theory]
    [InlineData(ChatMode.TextInput)]
    [InlineData(ChatMode.Conversation)]
    public async Task AProfileTemplate_KeepsItsVoiceWhateverTheChatMode(ChatMode chatMode)
    {
        // Arrange
        var driver = new AIProfileTemplateChatModeDisplayDriver(
            Mock.Of<IAIDeploymentManager>(),
            Mock.Of<IAIDeploymentCapabilityService>(),
            null,
            Mock.Of<ISiteService>(),
            Mock.Of<IRealtimeVoiceResolver>(),
            Mock.Of<IStringLocalizer<AIProfileTemplateChatModeDisplayDriver>>());
        var template = new AIProfileTemplate { Name = "voice-template", Source = AITemplateSources.Profile };

        // Act
        await driver.UpdateAsync(template, CreateContext(chatMode, "coral"));

        // Assert
        Assert.Equal("coral", template.GetOrCreate<ChatModeProfileSettings>().VoiceName);
    }

    [Fact]
    public async Task AProfile_WithAVoiceItsRealtimeDeploymentDoesNotOffer_IsRefused()
    {
        // Arrange
        // Live, a profile moved to OpenAI's realtime model kept an ElevenLabs voice. OpenAI refused the session's
        // set-up over it, so the calls ran with neither the profile's instructions nor its tools.
        var (deploymentManager, voiceResolver) = OpenAIRealtime();
        var driver = new AIProfileChatModeDisplayDriver(
            deploymentManager,
            Mock.Of<IAIDeploymentCapabilityService>(),
            null,
            Mock.Of<ISiteService>(),
            voiceResolver,
            new PassThroughLocalizer<AIProfileChatModeDisplayDriver>());
        var profile = new AIProfile { Type = AIProfileType.Chat };
        var context = CreateContext(ChatMode.Conversation, "EXAVITQu4vr4xnSDxMaL", "GptRealtime");

        // Act
        await driver.UpdateAsync(profile, context);

        // Assert
        var error = Assert.Single(context.Updater.ModelState.Values.SelectMany(entry => entry.Errors));
        Assert.Contains("GptRealtime", error.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("EXAVITQu4vr4xnSDxMaL", error.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AProfile_WithOneOfItsRealtimeDeploymentsVoices_IsSaved()
    {
        // Arrange
        var (deploymentManager, voiceResolver) = OpenAIRealtime();
        var driver = new AIProfileChatModeDisplayDriver(
            deploymentManager,
            Mock.Of<IAIDeploymentCapabilityService>(),
            null,
            Mock.Of<ISiteService>(),
            voiceResolver,
            new PassThroughLocalizer<AIProfileChatModeDisplayDriver>());
        var profile = new AIProfile { Type = AIProfileType.Chat };
        var context = CreateContext(ChatMode.Conversation, "marin", "GptRealtime");

        // Act
        await driver.UpdateAsync(profile, context);

        // Assert
        Assert.True(context.Updater.ModelState.IsValid);
        Assert.True(profile.TryGetSettings<ChatModeProfileSettings>(out var settings));
        Assert.Equal("marin", settings.VoiceName);
    }

    [Fact]
    public async Task AProfileTemplate_WithAVoiceItsRealtimeDeploymentDoesNotOffer_IsRefused()
    {
        // Arrange
        // The profiles a template creates take its voice, so they would carry the refusal to every call.
        var (deploymentManager, voiceResolver) = OpenAIRealtime();
        var driver = new AIProfileTemplateChatModeDisplayDriver(
            deploymentManager,
            Mock.Of<IAIDeploymentCapabilityService>(),
            null,
            Mock.Of<ISiteService>(),
            voiceResolver,
            new PassThroughLocalizer<AIProfileTemplateChatModeDisplayDriver>());
        var template = new AIProfileTemplate { Name = "voice-template", Source = AITemplateSources.Profile };
        var context = CreateContext(ChatMode.Conversation, "EXAVITQu4vr4xnSDxMaL", "GptRealtime");

        // Act
        await driver.UpdateAsync(template, context);

        // Assert
        Assert.False(context.Updater.ModelState.IsValid);
    }

    private static (IAIDeploymentManager DeploymentManager, IRealtimeVoiceResolver VoiceResolver) OpenAIRealtime()
    {
        var deployment = new AIDeployment { Name = "GptRealtime", ModelName = "gpt-realtime", ClientName = "OpenAI" };

        var deploymentManager = new Mock<IAIDeploymentManager>();
        deploymentManager.Setup(manager => manager.ResolveSlotAsync(
                AIDeploymentSlotNames.Realtime,
                "GptRealtime",
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(deployment);

        var voiceResolver = new Mock<IRealtimeVoiceResolver>();
        voiceResolver.Setup(resolver => resolver.GetVoicesAsync(deployment))
            .ReturnsAsync([new SpeechVoice { Id = "alloy", Name = "Alloy" }, new SpeechVoice { Id = "marin", Name = "Marin" }]);

        return (deploymentManager.Object, voiceResolver.Object);
    }

    private static UpdateEditorContext CreateContext(ChatMode chatMode, string voiceName, string conversationDeploymentName = null)
        => new(null, string.Empty, false, string.Empty, null, null, new FakeUpdateModel(new AIProfileChatModeViewModel
        {
            ChatMode = chatMode,
            VoiceName = voiceName,
            ConversationDeploymentName = conversationDeploymentName,
        }));

    // Formats the message with its arguments, so a test can read what the editor says.
    private sealed class PassThroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name]
            => new(name, name);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => [];
    }
}
