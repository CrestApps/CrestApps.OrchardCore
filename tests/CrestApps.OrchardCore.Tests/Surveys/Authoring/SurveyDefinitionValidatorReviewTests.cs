using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;

namespace CrestApps.OrchardCore.Tests.Surveys.Authoring;

/// <summary>
/// A scored survey can't let respondents reopen a response after its review showed them the key, or the score, which
/// gives the key away one changed answer at a time (R3-M12).
/// </summary>
public sealed class SurveyDefinitionValidatorReviewTests
{
    [Theory]
    [InlineData(ReviewShows.RightAnswers, ReviewShows.None)]
    [InlineData(ReviewShows.None, ReviewShows.Feedback)]
    [InlineData(ReviewShows.Score, ReviewShows.None)]
    [InlineData(ReviewShows.None, ReviewShows.Score | ReviewShows.Responses)]
    public async Task ValidateAsync_ScoredEditAfterSubmitWithTheKeyBeforeClose_IsAnError(ReviewShows immediatelyAfter, ReviewShows laterWhileOpen)
    {
        // Arrange
        var definition = Scored();
        definition.Review.ImmediatelyAfter = immediatelyAfter;
        definition.Review.LaterWhileOpen = laterWhileOpen;

        // Act
        var codes = await CodesAsync(definition);

        // Assert
        Assert.Contains("edit-after-review", codes);
    }

    [Fact]
    public async Task ValidateAsync_ScoredEditAfterSubmitWithTheKeyAfterCloseOnly_IsAllowed()
    {
        // Arrange
        var definition = Scored();
        definition.Review.ImmediatelyAfter = ReviewShows.Responses;
        definition.Review.LaterWhileOpen = ReviewShows.Responses;
        definition.Review.AfterClose = ReviewShows.All;

        // Act
        var codes = await CodesAsync(definition);

        // Assert
        Assert.DoesNotContain("edit-after-review", codes);
    }

    [Fact]
    public async Task ValidateAsync_ScoredEditAfterSubmitWithTheScoreHiddenFromRespondents_IsAllowed()
    {
        // Arrange: the review would show the score, but respondents never see it.
        var definition = Scored();
        definition.Scoring.ShowScoreToRespondent = false;
        definition.Review.ImmediatelyAfter = ReviewShows.Score | ReviewShows.Responses;
        definition.Review.LaterWhileOpen = ReviewShows.Score | ReviewShows.Responses;

        // Act
        var codes = await CodesAsync(definition);

        // Assert
        Assert.DoesNotContain("edit-after-review", codes);
    }

    [Theory]
    [InlineData("You scored {score}.", null)]
    [InlineData("You got {percent}%.", null)]
    [InlineData("Skills: {score:skill}.", null)]
    [InlineData("You scored {{ Score }}.", null)]
    [InlineData("{% if Response.Passed %}Well done{% endif %}", null)]
    [InlineData("You got {{ Response.Percent }}%.", null)]
    [InlineData(null, "/next?p={percent}")]
    public async Task R12M1_ScoredEditAfterSubmit_RefusesTheScoreInAnEnding(string message, string redirectUrl)
    {
        // Arrange: the review shows the key only after close, so edit-after-review allows the survey.
        var definition = KeyAfterCloseOnly();
        definition.Endings[0].Message = message;
        definition.Endings[0].RedirectUrl = redirectUrl;

        // Act
        var codes = await CodesAsync(definition);

        // Assert
        Assert.DoesNotContain("edit-after-review", codes);
        Assert.Contains("edit-after-score-ending", codes);
    }

    [Fact]
    public async Task R12M1_ScoredEditAfterSubmit_RefusesAnEndingChosenByTheScore()
    {
        // Arrange
        var definition = KeyAfterCloseOnly();
        definition.Endings.Add(new SurveyEnding
        {
            Id = "e2",
            Name = "passed",
            Condition = new SurveyVisibility
            {
                Mode = VisibilityMode.ShowWhen,
                Rule = new CrestApps.OrchardCore.Conditions.ConditionRule { Conditions = [new CrestApps.OrchardCore.Conditions.Condition { Source = "score", Operator = CrestApps.OrchardCore.Conditions.ConditionOperator.GreaterThanOrEqual, Values = ["4"] }] },
            },
        });

        // Act
        var codes = await CodesAsync(definition);

        // Assert
        Assert.Contains("edit-after-score-ending", codes);
    }

    [Theory]
    [InlineData("[percent] >= 50")]
    [InlineData("SCORE('q1') > 0")]
    [InlineData("[score:skill] > 1")]
    [InlineData("[var:grade] > 1")]
    public async Task R12M1_ScoredEditAfterSubmit_RefusesAnEndingWhoseFormulaReadsTheScore(string formula)
    {
        // Arrange: "grade" is a calculated variable over the score.
        var definition = KeyAfterCloseOnly();
        definition.Variables.Add(new SurveyVariable { Name = "grade", Kind = VariableKind.Calculated, Formula = "[score] * 2" });
        definition.Endings.Add(new SurveyEnding { Id = "e2", Name = "passed", Condition = new SurveyVisibility { Mode = VisibilityMode.ShowWhen, Formula = formula } });

        // Act
        var codes = await CodesAsync(definition);

        // Assert
        Assert.Contains("edit-after-score-ending", codes);
    }

    [Fact]
    public async Task R12M1_ScoreInAnEnding_IsAllowedWithoutEditAfterSubmit()
    {
        // Arrange
        var definition = KeyAfterCloseOnly();
        definition.Navigation.AllowEditAfterSubmit = false;
        definition.Endings[0].Message = "You scored {score} ({{ Response.Percent }}%).";

        // Act
        var codes = await CodesAsync(definition);

        // Assert
        Assert.DoesNotContain("edit-after-score-ending", codes);
    }

    private static SurveyDefinition KeyAfterCloseOnly()
    {
        var definition = Scored();
        definition.ScoreCategories.Add(new ScoreCategory { Id = "c1", Name = "skill" });
        definition.Review.ImmediatelyAfter = ReviewShows.Responses;
        definition.Review.LaterWhileOpen = ReviewShows.Responses;
        definition.Review.AfterClose = ReviewShows.All;

        return definition;
    }

    private static SurveyDefinition Scored()
    {
        var definition = AuthoringDefinitions.TwoPages();
        definition.Scoring.Enabled = true;
        definition.Navigation.AllowEditAfterSubmit = true;

        return definition;
    }

    private static async Task<IReadOnlyList<string>> CodesAsync(SurveyDefinition definition)
    {
        var validator = new SurveyDefinitionValidator(AuthoringSurveyHarness.CreateRegistry(), [], new NullSurveyFormulaValidator(), new AuthoringLocalizer<SurveyDefinitionValidator>());
        var context = await validator.ValidateAsync(null, definition, TestContext.Current.CancellationToken);

        return context.Messages.Where(message => message.IsError).Select(message => message.Code).ToList();
    }
}
