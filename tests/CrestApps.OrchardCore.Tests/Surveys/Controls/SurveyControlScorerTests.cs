using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core.Controls;
using CrestApps.OrchardCore.Surveys.Core.Controls.Boolean;
using CrestApps.OrchardCore.Surveys.Core.Controls.ChoiceBlanks;
using CrestApps.OrchardCore.Surveys.Core.Controls.FillInBlanks;
using CrestApps.OrchardCore.Surveys.Core.Controls.LongText;
using CrestApps.OrchardCore.Surveys.Core.Controls.Matching;
using CrestApps.OrchardCore.Surveys.Core.Controls.Matrix;
using CrestApps.OrchardCore.Surveys.Core.Controls.MultipleChoice;
using CrestApps.OrchardCore.Surveys.Core.Controls.Number;
using CrestApps.OrchardCore.Surveys.Core.Controls.Ranking;
using CrestApps.OrchardCore.Surveys.Core.Controls.ShortText;
using CrestApps.OrchardCore.Surveys.Core.Controls.SingleChoice;
using CrestApps.OrchardCore.Surveys.Models;
using static CrestApps.OrchardCore.Tests.Surveys.TestSupport.ControlsTestData;

namespace CrestApps.OrchardCore.Tests.Surveys.Controls;

/// <summary>
/// The built-in scorers, ported from the Learning assessment scorers, score an answer from the question's key alone.
/// </summary>
public sealed class SurveyControlScorerTests
{
    [Theory]
    [InlineData(new[] { "b" }, 2)]
    [InlineData(new[] { "a" }, 0)]
    [InlineData(new string[0], 0)]
    [InlineData(new[] { "a", "b" }, 0)]
    public void SingleChoice_Score_ScoresOnlyTheOneRightOption(string[] chosen, double expected)
    {
        var question = Question(SurveyControlNames.SingleChoice, ("a", "A"), ("b", "B"), ("c", "C"));
        question.Scoring = new SurveyQuestionScoring { Points = 2, CorrectChoiceIds = ["b"] };

        var result = new SingleChoiceScorer().Score(question, new SurveyAnswer { Values = chosen.ToList() }, ScoringContext());

        Assert.Equal((decimal)expected, result.Points);
        Assert.Equal(2, result.MaxPoints);
    }

    [Fact]
    public void SingleChoice_Score_UsesChoicePointsWhenSet()
    {
        var question = Question(SurveyControlNames.SingleChoice, ("a", "A"), ("b", "B"));
        question.Scoring = new SurveyQuestionScoring { ChoicePoints = new() { ["a"] = 1, ["b"] = 5 } };

        var result = new SingleChoiceScorer().Score(question, new SurveyAnswer { Values = ["a"] }, ScoringContext());

        Assert.Equal(1, result.Points);
        Assert.Equal(5, result.MaxPoints);
        Assert.False(result.Correct);
    }

    [Fact]
    public void SingleChoice_Score_TakesThePenaltyUnderNegativeMarking()
    {
        var question = Question(SurveyControlNames.SingleChoice, ("a", "A"), ("b", "B"));
        question.Scoring = new SurveyQuestionScoring { Points = 1, CorrectChoiceIds = ["b"], PenaltyPerWrong = 0.25m };

        var result = new SingleChoiceScorer().Score(question, new SurveyAnswer { Values = ["a"] }, ScoringContext(negativeMarking: true));

        Assert.Equal(-0.25m, result.Points);
    }

    [Theory]
    [InlineData(new[] { "a", "c" }, false, 3)]
    [InlineData(new[] { "a" }, false, 0)]
    [InlineData(new[] { "a" }, true, 1.5)]
    [InlineData(new[] { "a", "b" }, true, 0)]
    [InlineData(new[] { "a", "c", "b" }, true, 1.5)]
    [InlineData(new[] { "a", "c", "b" }, false, 0)]
    public void MultipleChoice_Score_IsAllOrNothingOrRightLessWrongWithPartialCredit(string[] chosen, bool partial, double expected)
    {
        var question = Question(SurveyControlNames.MultipleChoice, ("a", "A"), ("b", "B"), ("c", "C"), ("d", "D"));
        question.Scoring = new SurveyQuestionScoring { Points = 3, CorrectChoiceIds = ["a", "c"], PartialCredit = partial };

        var result = new MultipleChoiceScorer().Score(question, new SurveyAnswer { Values = chosen.ToList() }, ScoringContext());

        Assert.Equal((decimal)expected, result.Points);
    }

    [Theory]
    [InlineData("true", 1)]
    [InlineData("false", 0)]
    public void Boolean_Score_ScoresTheRightValue(string chosen, double expected)
    {
        var question = Question(SurveyControlNames.Boolean);
        question.Scoring = new SurveyQuestionScoring { CorrectChoiceIds = ["true"] };

        var result = new BooleanScorer().Score(question, new SurveyAnswer { Values = [chosen] }, ScoringContext());

        Assert.Equal((decimal)expected, result.Points);
    }

    [Fact]
    public void Boolean_Score_ReadsTheKeyTheImportersWrite()
    {
        var accepted = Question(SurveyControlNames.Boolean);
        accepted.Scoring = new SurveyQuestionScoring { AcceptedAnswers = ["false"] };
        var keyed = Question(SurveyControlNames.Boolean);
        keyed.Scoring = new SurveyQuestionScoring { Key = new System.Text.Json.Nodes.JsonObject { ["answer"] = "true" } };

        var fromAccepted = new BooleanScorer().Score(accepted, new SurveyAnswer { Values = ["false"] }, ScoringContext());
        var fromKey = new BooleanScorer().Score(keyed, new SurveyAnswer { Values = ["true"] }, ScoringContext());

        Assert.Equal(1, fromAccepted.Points);
        Assert.Equal(1, fromKey.Points);
    }

    [Fact]
    public void ChoiceBlanks_Score_FollowsTheCaseSetting()
    {
        var question = Question(SurveyControlNames.ChoiceBlanks);
        question.SetSettings(new ChoiceBlanksSettings { Text = "[[Cats]] purr.", CaseSensitive = true });

        var result = new ChoiceBlanksScorer().Score(question, new SurveyAnswer { Values = ["cats"] }, ScoringContext());

        Assert.Equal(0, result.Points);
    }

    [Theory]
    [InlineData("  Paris ", false, 1)]
    [InlineData("paris", false, 1)]
    [InlineData("paris", true, 0)]
    [InlineData("Lyon", false, 0)]
    [InlineData("", false, 0)]
    public void ShortText_Score_MatchesAnyAcceptedAnswerTrimmedAndWithCaseAsSet(string answer, bool caseSensitive, double expected)
    {
        var question = Question(SurveyControlNames.ShortText);
        question.Scoring = new SurveyQuestionScoring { AcceptedAnswers = ["Paris", "Paris, France"], CaseSensitive = caseSensitive };

        var result = new ShortTextScorer().Score(question, new SurveyAnswer { Values = [answer] }, ScoringContext());

        Assert.Equal((decimal)expected, result.Points);
    }

    [Theory]
    [InlineData(3.14, 1)]
    [InlineData(3.2, 1)]
    [InlineData(3.3, 0)]
    public void Number_Score_AcceptsAnAnswerWithinTheTolerance(double answer, double expected)
    {
        var question = Question(SurveyControlNames.Number);
        question.Scoring = new SurveyQuestionScoring { NumberAnswer = 3.14159m, Tolerance = 0.1m };

        var result = new NumberScorer().Score(question, new SurveyAnswer { Number = (decimal)answer }, ScoringContext());

        Assert.Equal((decimal)expected, result.Points);
    }

    [Fact]
    public void Matching_Score_GivesEachRightMatchItsShare()
    {
        var question = Question(SurveyControlNames.Matching, ("x", "X"), ("y", "Y"));
        question.Rows = [new SurveySubQuestion { Id = "r1", Text = "One" }, new SurveySubQuestion { Id = "r2", Text = "Two" }];
        question.Scoring = new SurveyQuestionScoring { Points = 2, PartialCredit = true, Pairs = new() { ["r1"] = "x", ["r2"] = "y" } };
        var answer = new SurveyAnswer { Rows = new() { ["r1"] = ["x"], ["r2"] = ["x"] } };

        var result = new MatchingScorer().Score(question, answer, ScoringContext());

        Assert.Equal(1, result.Points);
        Assert.False(result.Correct);
    }

    [Fact]
    public void Matrix_Score_IsAllOrNothingWithoutPartialCredit()
    {
        var question = Question(SurveyControlNames.Matrix, ("x", "X"), ("y", "Y"));
        question.Rows = [new SurveySubQuestion { Id = "r1" }, new SurveySubQuestion { Id = "r2" }];
        question.Scoring = new SurveyQuestionScoring { Points = 2, Pairs = new() { ["r1"] = "x", ["r2"] = "y" } };

        var right = new MatrixScorer().Score(question, new SurveyAnswer { Rows = new() { ["r1"] = ["x"], ["r2"] = ["y"] } }, ScoringContext());
        var half = new MatrixScorer().Score(question, new SurveyAnswer { Rows = new() { ["r1"] = ["x"], ["r2"] = ["x"] } }, ScoringContext());

        Assert.Equal(2, right.Points);
        Assert.Equal(0, half.Points);
    }

    [Theory]
    [InlineData(new[] { "a", "b", "c" }, false, 3)]
    [InlineData(new[] { "a", "c", "b" }, false, 0)]
    [InlineData(new[] { "a", "c", "b" }, true, 1)]
    public void Ranking_Score_ScoresTheRightOrder(string[] order, bool partial, double expected)
    {
        var question = Question(SurveyControlNames.Ranking, ("a", "A"), ("b", "B"), ("c", "C"));
        question.Scoring = new SurveyQuestionScoring { Points = 3, PartialCredit = partial };

        var result = new RankingScorer().Score(question, new SurveyAnswer { Values = order.ToList() }, ScoringContext());

        Assert.Equal((decimal)expected, result.Points);
    }

    [Fact]
    public void FillInBlanks_Score_GivesEachBlankItsShare()
    {
        var question = Question(SurveyControlNames.FillInBlanks);
        question.SetSettings(new FillInBlanksSettings { Text = "The [[sky|heavens]] is [[blue]]." });
        question.Scoring = new SurveyQuestionScoring { Points = 2 };

        var both = new FillInBlanksScorer().Score(question, new SurveyAnswer { Values = ["Heavens", "blue"] }, ScoringContext());
        var one = new FillInBlanksScorer().Score(question, new SurveyAnswer { Values = ["sea", "blue"] }, ScoringContext());

        Assert.Equal(2, both.Points);
        Assert.Equal(1, one.Points);
    }

    [Fact]
    public void ChoiceBlanks_Score_GivesEachBlankItsShare()
    {
        var question = Question(SurveyControlNames.ChoiceBlanks);
        question.SetSettings(new ChoiceBlanksSettings { Text = "[[Cats]] chase [[mice]].", Distractors = ["dogs"] });

        var result = new ChoiceBlanksScorer().Score(question, new SurveyAnswer { Values = ["Cats", "dogs"] }, ScoringContext());

        Assert.Equal(0.5m, result.Points);
    }

    [Fact]
    public void LongText_Score_LeavesAnAnswerToTheGraderAndScoresAnEmptyOneZero()
    {
        var question = Question(SurveyControlNames.LongText);
        question.Scoring = new SurveyQuestionScoring { Points = 5 };

        var answered = new LongTextScorer().Score(question, new SurveyAnswer { Values = ["An essay."] }, ScoringContext());
        var empty = new LongTextScorer().Score(question, null, ScoringContext());

        Assert.Null(answered.Points);
        Assert.Equal(5, answered.MaxPoints);
        Assert.Equal(0, empty.Points);
    }

    [Fact]
    public void GetFeedback_WrongAnswer_GivesTheRightAnswerAndTheIncorrectFeedback()
    {
        var question = Question(SurveyControlNames.SingleChoice, ("a", "Lyon"), ("b", "Paris"));
        question.Scoring = new SurveyQuestionScoring
        {
            CorrectChoiceIds = ["b"],
            IncorrectFeedback = "Not quite.",
            ChoiceFeedback = new() { ["a"] = "Lyon is the third city." },
        };

        var feedback = new SingleChoiceScorer().GetFeedback(question, new SurveyAnswer { Values = ["a"] });

        Assert.False(feedback.Correct);
        Assert.Equal("Paris", feedback.RightAnswer);
        Assert.Equal("Not quite.", feedback.Feedback);
        Assert.Equal("Lyon is the third city.", feedback.ChoiceFeedback["a"]);
    }
}
