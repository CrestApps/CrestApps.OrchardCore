using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Capabilities;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
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
            Mock.Of<IStringLocalizer<AIProfileTemplateChatModeDisplayDriver>>());
        var template = new AIProfileTemplate { Name = "voice-template", Source = AITemplateSources.Profile };

        // Act
        await driver.UpdateAsync(template, CreateContext(chatMode, "coral"));

        // Assert
        Assert.Equal("coral", template.GetOrCreate<ChatModeProfileSettings>().VoiceName);
    }

    private static UpdateEditorContext CreateContext(ChatMode chatMode, string voiceName)
        => new(null, string.Empty, false, string.Empty, null, null, new FakeUpdateModel(new AIProfileChatModeViewModel
        {
            ChatMode = chatMode,
            VoiceName = voiceName,
        }));
}
