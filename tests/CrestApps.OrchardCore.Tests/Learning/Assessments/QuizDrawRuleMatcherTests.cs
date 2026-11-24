using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Assessments.Models;

namespace CrestApps.OrchardCore.Tests.Learning.Assessments;

/// <summary>
/// The questions a "Draw at random" rule may draw, which an attempt draws from and the quiz editor counts: those with
/// the rule's tag whatever its case, and its difficulty, each only when the rule sets it.
/// </summary>
public sealed class QuizDrawRuleMatcherTests
{
    [Theory]
    [InlineData(null, null, true)]
    [InlineData("ladders", null, true)]
    [InlineData("LADDERS", null, true)]
    [InlineData("fire", null, false)]
    [InlineData(null, 2, true)]
    [InlineData(null, 3, false)]
    [InlineData("ladders", 2, true)]
    [InlineData("ladders", 4, false)]
    public void Matches_FollowsTheTagAndTheDifficulty(string tag, int? difficulty, bool expected)
    {
        // Arrange
        var question = new QuestionPart { Difficulty = 2, Tags = ["Ladders", "heights"] };
        var rule = new QuizDrawRule { BankId = "bank", Tag = tag, Difficulty = difficulty, Count = 1 };

        // Act
        var matches = QuizDrawRuleMatcher.Matches(question, rule);

        // Assert
        Assert.Equal(expected, matches);
    }

    [Fact]
    public void Matches_RefusesAQuestionWithoutItsPart_WhenTheRuleAsksForATagOrADifficulty()
    {
        Assert.True(QuizDrawRuleMatcher.Matches(null, new QuizDrawRule { BankId = "bank" }));
        Assert.False(QuizDrawRuleMatcher.Matches(null, new QuizDrawRule { BankId = "bank", Tag = "ladders" }));
        Assert.False(QuizDrawRuleMatcher.Matches(null, new QuizDrawRule { BankId = "bank", Difficulty = 1 }));
    }
}
