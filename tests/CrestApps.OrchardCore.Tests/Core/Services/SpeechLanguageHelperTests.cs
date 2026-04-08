using CrestApps.Core.AI.Services;

namespace CrestApps.OrchardCore.Tests.Core.Services;

public sealed class SpeechLanguageHelperTests
{
    [Theory]
    [InlineData(null, "en-US")]
    [InlineData("", "en-US")]
    [InlineData("en", "en-US")]
    [InlineData("en-US", "en-US")]
    [InlineData("fr", "fr-FR")]
    [InlineData("invalid-language", "en-US")]
    public void NormalizeOrDefault_ShouldReturnSpecificCultureOrFallback(string language, string expected)
    {
        var result = SpeechLanguageHelper.NormalizeOrDefault(language);

        Assert.Equal(expected, result);
    }
}
