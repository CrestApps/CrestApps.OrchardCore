using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;

namespace CrestApps.OrchardCore.Tests.Surveys.Authoring;

/// <summary>
/// A scored survey can't let respondents reopen a response after its review showed them the key.
/// </summary>
public sealed class SurveyDefinitionValidatorReviewTests
{
    [Theory]
    [InlineData(ReviewShows.RightAnswers, ReviewShows.None)]
    [InlineData(ReviewShows.None, ReviewShows.Feedback)]
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
        definition.Review.ImmediatelyAfter = ReviewShows.Score | ReviewShows.Responses;
        definition.Review.LaterWhileOpen = ReviewShows.Score | ReviewShows.Responses;
        definition.Review.AfterClose = ReviewShows.All;

        // Act
        var codes = await CodesAsync(definition);

        // Assert
        Assert.DoesNotContain("edit-after-review", codes);
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
