using CrestApps.OrchardCore.Surveys.Core.Indexes;

namespace CrestApps.OrchardCore.Tests.Surveys.Authoring;

/// <summary>
/// R8-Mi25: a tag is one line of text as the index holds it, so a line break in it never reaches a list, a filter or a
/// log line.
/// </summary>
public sealed class SurveyTagIndexProviderTests
{
    [Theory]
    [InlineData("north\r\nforged entry", "north  forged entry")]
    [InlineData("\nnorth\t", "north")]
    [InlineData("\r\n", "")]
    [InlineData("  plain  ", "plain")]
    [InlineData(null, "")]
    public void Normalize_StripsControlCharacters_AndTrims(string tag, string expected)
    {
        // Act
        var normalized = SurveyTagIndexProvider.Normalize(tag);

        // Assert
        Assert.Equal(expected, normalized);
        Assert.DoesNotContain(normalized, char.IsControl);
    }

    [Fact]
    public void Normalize_CutsALongTagAfterStrippingControlCharacters()
    {
        // Act
        var normalized = SurveyTagIndexProvider.Normalize("\r\n" + new string('a', SurveyTagIndexProvider.MaxTagLength + 10));

        // Assert
        Assert.Equal(SurveyTagIndexProvider.MaxTagLength, normalized.Length);
    }
}
