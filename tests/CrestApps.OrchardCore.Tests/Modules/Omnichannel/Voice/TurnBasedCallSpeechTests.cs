using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

public sealed class TurnBasedCallSpeechTests
{
    private const string ProviderDefaultVoice = "AWS.Polly.Joanna-Neural";

    [Fact]
    public async Task SelectCallSpeechAsync_WhenTheActivityPicksAVoice_ShouldUseItOverTheFlowAndThePersona()
    {
        // Arrange
        var activity = new OmnichannelActivity { TextToSpeechVoiceId = "activity-voice" };
        var flowSettings = new SubjectFlowSettings { TextToSpeechVoiceId = "flow-voice" };

        // Act
        var speech = await SelectAsync(PersonaVoiceTestServices.Create(), activity, flowSettings);

        // Assert
        Assert.Equal("activity-voice", speech.Voice);
        Assert.False(speech.IsPersonaVoice);
    }

    [Fact]
    public async Task SelectCallSpeechAsync_WhenTheFlowPicksAVoice_ShouldUseItOverThePersona()
    {
        // Arrange
        var flowSettings = new SubjectFlowSettings { TextToSpeechVoiceId = "flow-voice" };

        // Act
        var speech = await SelectAsync(PersonaVoiceTestServices.Create(), new OmnichannelActivity(), flowSettings);

        // Assert
        Assert.Equal("flow-voice", speech.Voice);
        Assert.False(speech.IsPersonaVoice);
    }

    [Fact]
    public async Task SelectCallSpeechAsync_WhenNoVoiceIsPickedAndThePersonaVoiceIsOfTheSpeakingProvider_ShouldUseThePersonaVoice()
    {
        // Arrange
        var activity = new OmnichannelActivity { TextToSpeechDeploymentName = PersonaVoiceTestServices.SameProviderDeployment };

        // Act
        var speech = await SelectAsync(PersonaVoiceTestServices.Create(), activity);

        // Assert
        Assert.Equal(PersonaVoiceTestServices.PersonaVoice, speech.Voice);
        Assert.True(speech.IsPersonaVoice);
    }

    [Fact]
    public async Task SelectCallSpeechAsync_WhenThePersonaVoiceIsOfAnotherProvider_ShouldUseTheProviderDefaultVoice()
    {
        // Arrange
        var flowSettings = new SubjectFlowSettings { TextToSpeechDeploymentName = PersonaVoiceTestServices.OtherProviderDeployment };

        // Act
        var speech = await SelectAsync(PersonaVoiceTestServices.Create(), new OmnichannelActivity(), flowSettings);

        // Assert
        Assert.Equal(ProviderDefaultVoice, speech.Voice);
        Assert.False(speech.IsPersonaVoice);
    }

    [Fact]
    public async Task SelectCallSpeechAsync_WhenOnlyTheSiteNamesASpeakingDeployment_ShouldCompareThePersonaVoiceWithIt()
    {
        // Arrange
        var services = PersonaVoiceTestServices.Create(configure: collection => collection.AddSingleton(CreateSiteService(PersonaVoiceTestServices.OtherProviderDeployment)));

        // Act
        var speech = await SelectAsync(services, new OmnichannelActivity());

        // Assert
        Assert.Equal(ProviderDefaultVoice, speech.Voice);
    }

    [Fact]
    public async Task SelectCallSpeechAsync_WhenTheAIPersonasFeatureIsDisabled_ShouldSpeakEnglishWithTheProviderDefaultVoice()
    {
        // Act
        var speech = await SelectAsync(PersonaVoiceTestServices.Create(withPersonas: false), new OmnichannelActivity());

        // Assert
        Assert.Equal(ProviderDefaultVoice, speech.Voice);
        Assert.Equal("en-US", speech.SpeakLanguage);
        Assert.Equal("en", speech.ListenLanguage);
        Assert.False(speech.UsesPersona);
    }

    [Fact]
    public async Task SelectCallSpeechAsync_WhenOutsideAShellScope_ShouldUseTheProviderDefaultVoice()
    {
        // Act
        var speech = await VoiceAgentConversationLoop.SelectCallSpeechAsync(
            services: null,
            new OmnichannelActivity(),
            flowSettings: null,
            PersonaVoiceTestServices.CreateProfile(),
            ProviderDefaultVoice,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ProviderDefaultVoice, speech.Voice);
        Assert.False(speech.UsesPersona);
    }

    [Fact]
    public async Task SelectCallSpeechAsync_WhenThePersonaHasALanguage_ShouldSpeakAndListenInIt()
    {
        // Arrange
        var activity = new OmnichannelActivity { TextToSpeechVoiceId = "activity-voice" };

        // Act
        var speech = await SelectAsync(PersonaVoiceTestServices.Create(personaLanguage: "es-MX"), activity);

        // Assert
        Assert.Equal("activity-voice", speech.Voice);
        Assert.Equal("es-MX", speech.SpeakLanguage);
        Assert.Equal("es", speech.ListenLanguage);
        Assert.True(speech.UsesPersona);
    }

    [Fact]
    public void WithoutPersona_ShouldSpeakEnglishWithTheGivenVoice()
    {
        // Act
        var speech = VoiceAgentConversationLoop.CallSpeech.WithoutPersona(ProviderDefaultVoice);

        // Assert
        Assert.Equal(ProviderDefaultVoice, speech.Voice);
        Assert.Equal("en-US", speech.SpeakLanguage);
        Assert.False(speech.UsesPersona);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("en-GB", true)]
    [InlineData("el-GR", false)]
    [InlineData("es-MX", false)]
    public void IsEnglish_ShouldFollowThePersonasLanguage(string personaLanguage, bool expected)
    {
        // Act
        var speech = new VoiceAgentConversationLoop.CallSpeech(ProviderDefaultVoice, personaLanguage);

        // Assert
        Assert.Equal(expected, speech.IsEnglish);
    }

    private static Task<VoiceAgentConversationLoop.CallSpeech> SelectAsync(
        IServiceProvider services,
        OmnichannelActivity activity,
        SubjectFlowSettings flowSettings = null)
    {
        return VoiceAgentConversationLoop.SelectCallSpeechAsync(
            services,
            activity,
            flowSettings,
            PersonaVoiceTestServices.CreateProfile(),
            ProviderDefaultVoice,
            TestContext.Current.CancellationToken);
    }

    private static ISiteService CreateSiteService(string textToSpeechDeploymentName)
    {
        var site = new Mock<ISite>();
        site.Setup(currentSite => currentSite.GetOrCreate<DefaultAIDeploymentSettings>())
            .Returns(new DefaultAIDeploymentSettings { DefaultTextToSpeechDeploymentName = textToSpeechDeploymentName });

        var siteService = new Mock<ISiteService>();
        siteService.Setup(service => service.GetSiteSettingsAsync())
            .ReturnsAsync(site.Object);

        return siteService.Object;
    }
}
