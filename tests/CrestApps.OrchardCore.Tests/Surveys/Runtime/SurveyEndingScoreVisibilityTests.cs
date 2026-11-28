using CrestApps.OrchardCore.Surveys.Core.Logic;
using CrestApps.OrchardCore.Surveys.Core.Runtime;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Infrastructure.Html;

namespace CrestApps.OrchardCore.Tests.Surveys.Runtime;

/// <summary>
/// An ending shows the score (piped or written by Liquid) only when the review at that moment shows the respondent the
/// score, so an ending can't give the key away one answer at a time while the review hides it (R12-M1).
/// </summary>
public sealed class SurveyEndingScoreVisibilityTests
{
    private const string PipedMessage = "<p>Score [{score}] percent [{percent}] skill [{score:skill}]</p>";

    [Fact]
    public async Task R12M1_Ending_WhenTheReviewHidesTheScore_PipesNoScore()
    {
        // Arrange: right after finishing the review shows the responses only.
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var (survey, definition, response) = Finished(ReviewShows.Responses);

        // Act
        var ending = await harness.RunAsync(services => services.GetRequiredService<SurveyEndingService>().RenderAsync(survey, definition, response, "site.example"));

        // Assert
        Assert.Contains("Score [] percent [] skill []", ending.MessageHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task R12M1_Ending_WhenTheReviewShowsTheScore_PipesTheScore()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var (survey, definition, response) = Finished(ReviewShows.Score | ReviewShows.Responses);

        // Act
        var ending = await harness.RunAsync(services => services.GetRequiredService<SurveyEndingService>().RenderAsync(survey, definition, response, "site.example"));

        // Assert
        Assert.Contains("Score [4] percent [50] skill [3]", ending.MessageHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task R12M1_Ending_WhenRespondentsNeverSeeTheScore_PipesNoScore()
    {
        // Arrange: the review would show the score, but the survey hides it from respondents.
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var (survey, definition, response) = Finished(ReviewShows.Score | ReviewShows.Responses);
        definition.Scoring.ShowScoreToRespondent = false;

        // Act
        var ending = await harness.RunAsync(services => services.GetRequiredService<SurveyEndingService>().RenderAsync(survey, definition, response, "site.example"));

        // Assert
        Assert.Contains("Score [] percent [] skill []", ending.MessageHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task R12M1_LiquidEnding_WhenTheScoreIsHidden_WritesNoScorePercentOrPass()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var (survey, definition, response) = Finished(ReviewShows.Responses);
        var ending = new SurveyEnding
        {
            Id = "end",
            Name = "end",
            Message = "<p>Score [{{ Score }}] percent [{{ Response.Percent }}] {% if Response.Passed %}PASSED{% endif %}</p>",
        };

        // Act
        var hidden = await RenderLiquidAsync(harness, survey, definition, response, ending, showScore: false);
        var shown = await RenderLiquidAsync(harness, survey, definition, response, ending, showScore: true);

        // Assert
        Assert.Contains("Score [] percent []", hidden, StringComparison.Ordinal);
        Assert.DoesNotContain("PASSED", hidden, StringComparison.Ordinal);
        Assert.Contains("Score [4] percent [50] PASSED", shown, StringComparison.Ordinal);
    }

    private static (Survey Survey, SurveyDefinition Definition, SurveyResponse Response) Finished(ReviewShows immediatelyAfter)
    {
        var definition = RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1")));
        definition.Scoring.Enabled = true;
        definition.ScoreCategories.Add(new ScoreCategory { Id = "c1", Name = "skill" });
        definition.Review.ImmediatelyAfter = immediatelyAfter;
        definition.Endings[0].Message = PipedMessage;

        var survey = new Survey { ItemId = "s1", Name = "s", Title = "Survey", Status = SurveyStatus.Open };
        var response = new SurveyResponse
        {
            ItemId = "r1",
            SurveyId = "s1",
            Status = SurveyResponseStatus.Completed,
            CompletedUtc = RuntimeTestHarness.Now,
            EndingId = "end",
            Grading = GradingStatus.Graded,
            Score = 4,
            MaxScore = 8,
            Percent = 50,
            Passed = true,
        };

        response.CategoryScores["c1"] = 3;

        return (survey, definition, response);
    }

    private static Task<string> RenderLiquidAsync(RuntimeTestHarness harness, Survey survey, SurveyDefinition definition, SurveyResponse response, SurveyEnding ending, bool showScore)
        => harness.RunAsync(services =>
        {
            var renderer = new LiquidSurveyEndingRenderer(
                services.GetRequiredService<SurveyPipingRenderer>(),
                new HtmlSanitizerService(Options.Create(new HtmlSanitizerOptions())),
                services.GetRequiredService<ISurveyControlRegistry>(),
                Mock.Of<global::OrchardCore.Modules.ILocalClock>(clock => clock.GetLocalTimeZoneAsync() == Task.FromResult<global::OrchardCore.Modules.ITimeZone>(null)),
                NullLogger<LiquidSurveyEndingRenderer>.Instance);

            return renderer.RenderAsync(survey, definition, response, ending, showScore);
        });
}
