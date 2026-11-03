using CrestApps.OrchardCore.Omnichannel.Voice.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// The language a call's summary and handoff briefing are written in.
/// </summary>
public sealed class VoiceCallLanguageTests
{
    [Theory]
    [InlineData("el-GR", "Greek")]
    [InlineData("el", "Greek")]
    [InlineData(" es-MX ", "Spanish")]
    [InlineData("en-US", "English")]
    public void GetName_WhenGivenALanguageTag_ShouldNameItsLanguageInEnglish(string language, string expected)
    {
        // Act
        var name = VoiceCallLanguage.GetName(language);

        // Assert
        Assert.Equal(expected, name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void GetName_WhenGivenNoLanguage_ShouldReturnNull(string language)
    {
        // Act
        var name = VoiceCallLanguage.GetName(language);

        // Assert
        Assert.Null(name);
    }

    [Fact]
    public async Task GetNameAsync_WhenThePersonaHasALanguage_ShouldNameIt()
    {
        // Act
        var name = await VoiceCallLanguage.GetNameAsync(
            PersonaVoiceTestServices.Create(personaLanguage: "el-GR"),
            PersonaVoiceTestServices.CreateProfile(),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Greek", name);
    }

    [Fact]
    public async Task GetNameAsync_WhenTheAIPersonasFeatureIsDisabled_ShouldReturnNull()
    {
        // Act
        var name = await VoiceCallLanguage.GetNameAsync(
            PersonaVoiceTestServices.Create(withPersonas: false),
            PersonaVoiceTestServices.CreateProfile(),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(name);
    }
}
