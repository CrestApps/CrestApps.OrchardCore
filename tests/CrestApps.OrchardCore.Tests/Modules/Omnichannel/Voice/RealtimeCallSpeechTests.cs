using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// The voice a live call is spoken in, and the language its caller is transcribed in.
/// </summary>
/// <remarks>
/// Live, a Greek profile's call sent no language to the transcription, which guessed each phrase's language on its
/// own: the caller's greeting came back as Finnish, and the summary and outcome were written from that transcript.
/// </remarks>
public sealed class RealtimeCallSpeechTests
{
    [Fact]
    public async Task SelectCallSpeechAsync_WhenThePersonaHasALanguage_ShouldTranscribeTheCallerInItsLanguagePart()
    {
        // Act
        var (_, speechLanguage) = await SelectAsync(PersonaVoiceTestServices.Create(personaLanguage: "el-GR"), CreateContext());

        // Assert
        Assert.Equal("el", speechLanguage);
    }

    [Fact]
    public async Task SelectCallSpeechAsync_WhenTheActivityPicksAVoice_ShouldStillTranscribeInThePersonasLanguage()
    {
        // Arrange
        var context = CreateContext();
        context.Activity.TextToSpeechVoiceId = " activity-voice ";

        // Act
        var (voice, speechLanguage) = await SelectAsync(PersonaVoiceTestServices.Create(personaLanguage: "el-GR"), context);

        // Assert
        Assert.Equal("activity-voice", voice);
        Assert.Equal("el", speechLanguage);
    }

    [Fact]
    public async Task SelectCallSpeechAsync_WhenThePersonaHasNoLanguage_ShouldLeaveTheLanguageToTheModel()
    {
        // Act
        var (_, speechLanguage) = await SelectAsync(PersonaVoiceTestServices.Create(), CreateContext());

        // Assert
        Assert.Null(speechLanguage);
    }

    [Fact]
    public async Task SelectCallSpeechAsync_WhenTheAIPersonasFeatureIsDisabled_ShouldUseTheProfilesVoiceAndNoLanguage()
    {
        // Arrange
        var context = CreateContext();
        context.Profile.AlterSettings<ChatModeProfileSettings>(settings => settings.VoiceName = "coral");

        // Act
        var (voice, speechLanguage) = await SelectAsync(PersonaVoiceTestServices.Create(withPersonas: false), context);

        // Assert
        Assert.Equal("coral", voice);
        Assert.Null(speechLanguage);
    }

    [Fact]
    public async Task SelectCallSpeechAsync_WhenOutsideAShellScope_ShouldUseTheProfilesVoiceAndNoLanguage()
    {
        // Arrange
        var context = CreateContext();
        context.Profile.AlterSettings<ChatModeProfileSettings>(settings => settings.VoiceName = "coral");

        // Act
        var (voice, speechLanguage) = await SelectAsync(services: null, context);

        // Assert
        Assert.Equal("coral", voice);
        Assert.Null(speechLanguage);
    }

    private static Task<(string Voice, string SpeechLanguage)> SelectAsync(IServiceProvider services, RealtimeVoiceConversationContext context)
    {
        return RealtimeVoiceConversationRunner.SelectCallSpeechAsync(services, context, TestContext.Current.CancellationToken);
    }

    private static RealtimeVoiceConversationContext CreateContext()
    {
        return new RealtimeVoiceConversationContext
        {
            Activity = new OmnichannelActivity(),
            Profile = PersonaVoiceTestServices.CreateProfile(),
            RealtimeDeploymentName = PersonaVoiceTestServices.SameProviderDeployment,
        };
    }
}
