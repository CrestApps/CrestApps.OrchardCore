using CrestApps.OrchardCore.Surveys.AI.Services;

namespace CrestApps.OrchardCore.Tests.Surveys.AI;

/// <summary>
/// Respondent answers given to a model can't smuggle a working link through the cleaning: markup, invisible
/// characters, entities and lookalike dots are dealt with before links are matched, and domains are matched by shape.
/// </summary>
public sealed class SurveyRespondentTextTests
{
    [Theory]
    [InlineData("go to ht*tps://evil.example/x today")]
    [InlineData("visit evil.c*om today")]
    [InlineData("visit evil.c\u00ADom today")]
    [InlineData("visit evil\u200B.com today")]
    [InlineData("visit evil&#46;com today")]
    [InlineData("visit evil&amp;#46;com today")]
    [InlineData("visit evil\uFF0Ecom today")]
    [InlineData("visit evil\u3002com today")]
    [InlineData("visit \uFF45\uFF56\uFF49\uFF4C.com today")]
    [InlineData("visit evil.ai today")]
    [InlineData("visit evil.gg/path today")]
    [InlineData("visit evil.example today")]
    [InlineData("visit <a href=\"x\">evil.zone</a> today")]
    [InlineData("visit [evil](https://evil.example) today")]
    [InlineData("visit evil[.]com today")]
    [InlineData("run javascript:alert(1) today")]
    [InlineData("visit //evil.example today")]
    [InlineData("visit 203.0.113.9/x today")]
    public void Clean_NeutralizesHiddenLinks(string answer)
    {
        // Act
        var cleaned = SurveyRespondentText.Clean(answer);

        // Assert
        Assert.DoesNotContain("evil.", cleaned, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("://", cleaned, StringComparison.Ordinal);
        Assert.DoesNotContain("javascript:", cleaned, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("203.0.113.9", cleaned, StringComparison.Ordinal);
        Assert.StartsWith(answer.Split(' ')[0], cleaned, StringComparison.Ordinal);
        Assert.EndsWith("today", cleaned, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("I'd give it 3.14 out of 5, e.g. for speed.", "I'd give it 3.14 out of 5, e.g. for speed.")]
    [InlineData("Great  **service**,\nthanks!", "Great service, thanks!")]
    [InlineData("Fish &amp; chips", "Fish & chips")]
    public void Clean_KeepsOrdinaryText(string answer, string expected)
        => Assert.Equal(expected, SurveyRespondentText.Clean(answer));

    [Fact]
    public void Clean_NeverSplitsASurrogatePairWhenShortening()
    {
        // Arrange: an emoji (a surrogate pair) straddles the cut.
        var answer = new string('a', SurveyRespondentText.MaxLength - 1) + "\U0001F600" + "tail";

        // Act
        var cleaned = SurveyRespondentText.Clean(answer);

        // Assert
        Assert.EndsWith("…", cleaned, StringComparison.Ordinal);
        Assert.False(char.IsHighSurrogate(cleaned[^2]));
        Assert.Equal(SurveyRespondentText.MaxLength - 1, cleaned.Length - 1);
    }
}
