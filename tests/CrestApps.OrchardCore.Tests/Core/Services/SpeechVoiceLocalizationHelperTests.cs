using System.Globalization;
using CrestApps.Core.AI.Services;

namespace CrestApps.OrchardCore.Tests.Core.Services;

public sealed class SpeechVoiceLocalizationHelperTests
{
    [Fact]
    public void CreateAllowedCultures_WithNoSupportedCultures_IncludesCurrentCultureHierarchy()
    {
        var cultures = SpeechVoiceLocalizationHelper.CreateAllowedCultures(
            [],
            new CultureInfo("en-US"));

        Assert.Contains("en-US", cultures);
        Assert.Contains("en", cultures);
    }

    [Theory]
    [InlineData("en-US", true)]
    [InlineData("en-GB", true)]
    [InlineData("fr-FR", false)]
    public void IsLanguageAllowed_MatchesCurrentCultureHierarchy(string language, bool expected)
    {
        var allowedCultures = SpeechVoiceLocalizationHelper.CreateAllowedCultures(
            [],
            new CultureInfo("en-US"));

        var result = SpeechVoiceLocalizationHelper.IsLanguageAllowed(language, allowedCultures);

        Assert.Equal(expected, result);
    }
}
