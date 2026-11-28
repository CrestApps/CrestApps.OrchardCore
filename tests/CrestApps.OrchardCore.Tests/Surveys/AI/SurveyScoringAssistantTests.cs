using System.Security.Claims;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.AI;
using CrestApps.OrchardCore.Surveys.AI.Services;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Results;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Moq;

namespace CrestApps.OrchardCore.Tests.Surveys.AI;

public sealed class SurveyScoringAssistantTests
{
    private static readonly ClaimsPrincipal _grader = new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "grader1")], "Test"));

    [Fact]
    public async Task SuggestAsync_ForAnEssay_ReturnsTheSuggestionAndRecordsTheHelp()
    {
        // Arrange
        var fixture = new Fixture(manuallyGraded: true, allowed: true, answer: "Fire exits are on each floor.");
        fixture.Completer
            .Setup(completer => completer.CompleteAsync(SurveysAIConstants.Templates.ScoreAnswer, It.IsAny<IDictionary<string, object>>(), It.Is<string>(input => input.Contains("Fire exits")), It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"points\": 4, \"comment\": \"Mentions the exits.\"}");

        // Act
        var result = await fixture.Create().SuggestAsync(_grader, "r1", "q1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result.Error);
        Assert.Equal(4m, result.Suggestion.Points);
        fixture.Grading.Verify(grading => grading.RecordScoringHelpAsync("r1", "q1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SuggestAsync_ForAQuestionThatNeedsNoGrader_ReturnsNotFoundWithoutAskingTheModel()
    {
        // Arrange
        var fixture = new Fixture(manuallyGraded: false, allowed: true, answer: "Yes");

        // Act
        var result = await fixture.Create().SuggestAsync(_grader, "r1", "q1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SurveyScoringHelpResult.NotFound, result.Error);
        fixture.Completer.Verify(completer => completer.CompleteAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SuggestAsync_WhenTheUserMayNotGrade_ReturnsNotFoundAndRecordsNothing()
    {
        // Arrange
        var fixture = new Fixture(manuallyGraded: true, allowed: false, answer: "Text");

        // Act
        var result = await fixture.Create().SuggestAsync(_grader, "r1", "q1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SurveyScoringHelpResult.NotFound, result.Error);
        fixture.Grading.Verify(grading => grading.RecordScoringHelpAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SuggestAsync_WithoutAModel_ReturnsNoModel()
    {
        // Arrange
        var fixture = new Fixture(manuallyGraded: true, allowed: true, answer: "Text");

        // Act
        var result = await fixture.Create().SuggestAsync(_grader, "r1", "q1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SurveyScoringHelpResult.NoModel, result.Error);
    }

    private sealed class Fixture
    {
        public Fixture(bool manuallyGraded, bool allowed, string answer)
        {
            var survey = new Survey { ItemId = "s1", Name = "quiz" };
            var question = new SurveyQuestion { Id = "q1", Name = "q_essay", ControlType = "Essay", Title = "Where are the exits?", Scoring = new SurveyQuestionScoring { Points = 5, GraderGuidance = "Name the exits." } };
            var response = new SurveyResponse
            {
                ItemId = "r1",
                SurveyId = "s1",
                SurveyVersionId = "v1",
                Status = SurveyResponseStatus.Completed,
                Answers = { ["q1"] = new SurveyAnswer { Values = [answer], MaxPoints = 5 } },
            };
            var version = new SurveyVersion { ItemId = "v1", SurveyId = "s1", Definition = new SurveyDefinition { Pages = [new SurveyPage { Id = "p1", Questions = [question] }] } };

            Responses.Setup(queries => queries.FindByIdAsync("r1", It.IsAny<CancellationToken>())).ReturnsAsync(response);
            Surveys.Setup(manager => manager.FindByIdAsync("s1", It.IsAny<CancellationToken>())).ReturnsAsync(survey);
            Versions.Setup(cache => cache.GetAsync("v1", It.IsAny<CancellationToken>())).ReturnsAsync(version);
            Authorization.Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), survey, SurveyOperation.Grade, It.IsAny<CancellationToken>())).ReturnsAsync(allowed);
            Registry.Setup(registry => registry.GetControl("Essay")).Returns(new SurveyControlDescriptor
            {
                Name = "Essay",
                Capabilities = SurveyControlCapabilities.Answerable | (manuallyGraded ? SurveyControlCapabilities.ManuallyGraded : SurveyControlCapabilities.None),
            });
        }

        public Mock<ISurveysAICompleter> Completer { get; } = new();

        public Mock<ISurveyResponseQueries> Responses { get; } = new();

        public Mock<ISurveyVersionCache> Versions { get; } = new();

        public Mock<ISurveyManager> Surveys { get; } = new();

        public Mock<ISurveyAuthorizationService> Authorization { get; } = new();

        public Mock<ISurveyControlRegistry> Registry { get; } = new();

        public Mock<ISurveyGradingService> Grading { get; } = new();

        public SurveyScoringAssistant Create()
            => new(Completer.Object, Responses.Object, Versions.Object, Surveys.Object, Authorization.Object, Registry.Object, Grading.Object, new SurveyAnonymousGroups(new SurveyResultsQueries(new Mock<global::YesSql.ISession> { DefaultValue = DefaultValue.Mock }.Object), Mock.Of<IServiceProvider>()));
    }
}
