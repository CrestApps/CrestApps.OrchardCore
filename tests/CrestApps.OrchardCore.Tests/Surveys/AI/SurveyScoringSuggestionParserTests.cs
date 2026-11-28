using CrestApps.OrchardCore.Surveys.AI.Services;

namespace CrestApps.OrchardCore.Tests.Surveys.AI;

public sealed class SurveyScoringSuggestionParserTests
{
    [Fact]
    public void Parse_WithAWrappedAnswer_ReadsThePointsAndTheComment()
    {
        // Act
        var suggestion = SurveyScoringSuggestionParser.Parse("Here it is:\n```json\n{\"points\": 3.456, \"comment\": \"Clear answer.\"}\n```", 5);

        // Assert
        Assert.Equal(3.46m, suggestion.Points);
        Assert.Equal("Clear answer.", suggestion.Comment);
    }

    [Fact]
    public void Parse_WithPointsAboveTheMaximum_KeepsOnlyTheComment()
    {
        // Act
        var suggestion = SurveyScoringSuggestionParser.Parse("{\"points\": 9, \"comment\": \"Good.\"}", 5);

        // Assert
        Assert.Null(suggestion.Points);
        Assert.Equal("Good.", suggestion.Comment);
    }

    [Theory]
    [InlineData("no json here")]
    [InlineData("{\"points\": -1}")]
    [InlineData("{}")]
    public void Parse_WithoutAUsableSuggestion_ReturnsNull(string text)
        => Assert.Null(SurveyScoringSuggestionParser.Parse(text, 5));

    [Fact]
    public void Parse_WithPointsWrittenAsText_ReadsThem()
        => Assert.Equal(2m, SurveyScoringSuggestionParser.Parse("{\"points\": \"2\"}", 5).Points);
}
