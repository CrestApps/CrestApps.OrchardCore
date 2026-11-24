using CrestApps.OrchardCore.Learning.AI.Services;
using CrestApps.OrchardCore.Learning.Coursework.Models;

namespace CrestApps.OrchardCore.Tests.Learning.AI;

/// <summary>
/// Scoring help keeps only a suggestion the grading form accepts: points in range, real rubric levels and feedback of
/// the form's length. It never invents a score it cannot read.
/// </summary>
public sealed class EssayScoringParserTests
{
    private static readonly RubricCriterion[] _rubric =
    [
        new RubricCriterion
        {
            Id = "c1",
            Title = "Clarity",
            Levels = [new RubricLevel { Id = "c1l1", Title = "Weak", Points = 0 }, new RubricLevel { Id = "c1l2", Title = "Strong", Points = 4 }],
        },
        new RubricCriterion
        {
            Id = "c2",
            Title = "Depth",
            Levels = [new RubricLevel { Id = "c2l1", Title = "Thin", Points = 0 }, new RubricLevel { Id = "c2l2", Title = "Deep", Points = 4 }],
        },
    ];

    [Fact]
    public void Points_WithinRange_AreSuggestedWithTheFeedback()
    {
        var suggestion = EssayScoringParser.Parse("""```json {"points": 3.456, "feedback": "Good start."} ```""", 5, null, 1000);

        Assert.NotNull(suggestion);
        Assert.Equal(3.46m, suggestion.Points);
        Assert.Equal("Good start.", suggestion.Feedback);
    }

    [Theory]
    [InlineData("""{"points": 9, "feedback": "Too many"}""")]
    [InlineData("""{"points": -1, "feedback": "Below zero"}""")]
    [InlineData("""{"points": "lots", "feedback": "Not a number"}""")]
    public void Points_OutOfRangeOrNotANumber_AreLeftOut_ButTheFeedbackStays(string answer)
    {
        var suggestion = EssayScoringParser.Parse(answer, 5, null, 1000);

        Assert.NotNull(suggestion);
        Assert.Null(suggestion.Points);
        Assert.NotNull(suggestion.Feedback);
    }

    [Fact]
    public void RubricChoices_KeepOnlyLevelsOfTheirOwnCriterion()
    {
        var suggestion = EssayScoringParser.Parse("""{"choices": {"c1": "c1l2", "c2": "c1l1", "c3": "x"}, "feedback": "Clear but thin.", "points": 5}""", 8, _rubric, 1000);

        Assert.NotNull(suggestion);
        Assert.Equal(new Dictionary<string, string> { ["c1"] = "c1l2" }, suggestion.Choices);
        Assert.Null(suggestion.Points);
    }

    [Fact]
    public void Feedback_IsCutToTheFormsLength()
    {
        var suggestion = EssayScoringParser.Parse("{\"points\": 1, \"feedback\": \"" + new string('a', 50) + "\"}", 5, null, 10);

        Assert.Equal(10, suggestion.Feedback.Length);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Full marks!")]
    [InlineData("{\"points\": 99}")]
    public void NothingUsable_IsNoSuggestion(string answer)
    {
        Assert.Null(EssayScoringParser.Parse(answer, 5, null, 1000));
    }
}
