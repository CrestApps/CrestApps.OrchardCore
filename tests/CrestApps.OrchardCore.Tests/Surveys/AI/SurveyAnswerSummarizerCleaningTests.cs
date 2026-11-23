using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.AI.Services;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Moq;

namespace CrestApps.OrchardCore.Tests.Surveys.AI;

/// <summary>
/// Production review P4-L8: the answer summarizer cleans respondents' answers (links and markup removed, shortened)
/// before the model reads them, with the untrusted-text notice, as the results tool does.
/// </summary>
public sealed class SurveyAnswerSummarizerCleaningTests
{
    [Fact]
    public async Task P4L8_TheSummarizer_SendsCleanedAnswers_WithTheNotice()
    {
        // Arrange
        var survey = new Survey { ItemId = "survey1", Title = "Feedback" };
        var definition = new SurveyDefinition
        {
            Pages = [new SurveyPage { Id = "p1", Questions = [new SurveyQuestion { Id = "q1", Name = "q1", ControlType = "LongText", Title = "What should change?" }] }],
        };

        var surveys = new Mock<ISurveyManager>();
        surveys.Setup(manager => manager.FindByIdAsync("survey1", It.IsAny<CancellationToken>())).ReturnsAsync(survey);

        var versions = new Mock<ISurveyVersionCache>();
        versions.Setup(cache => cache.GetPublishedAsync(survey, It.IsAny<CancellationToken>())).ReturnsAsync(new SurveyVersion { Definition = definition });

        var authorization = new Mock<ISurveyAuthorizationService>();
        authorization.Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), survey, SurveyOperation.ViewResponses, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var aggregates = new Mock<ISurveyAnswerAggregates>();
        aggregates.Setup(service => service.TextSamplesAsync(It.IsAny<SurveyAnswerFilter>(), "q1", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(["Great course, see https://evil.example/claim for a prize", "<b>More</b> practice please"]);

        var registry = new Mock<ISurveyControlRegistry>();
        registry.Setup(service => service.GetControl("LongText")).Returns(new SurveyControlDescriptor
        {
            Name = "LongText",
            Category = SurveyControlCategories.Text,
            Capabilities = SurveyControlCapabilities.Answerable,
        });

        string input = null;
        var completer = new Mock<ISurveysAICompleter>();
        completer.Setup(service => service.CompleteAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((string _, IDictionary<string, object> _, string text, CancellationToken _) => input = text)
            .ReturnsAsync("Summary");

        var summarizer = new SurveyAnswerSummarizer(completer.Object, surveys.Object, versions.Object, authorization.Object, aggregates.Object, registry.Object, null);

        // Act
        var result = await summarizer.SummarizeAsync(new ClaimsPrincipal(new ClaimsIdentity("Test")), "survey1", "q1", "en", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Summary", result.Summary);
        var json = JsonNode.Parse(input)!.AsObject();
        var answers = json["answers"]!.AsArray().Select(node => node!.GetValue<string>()).ToList();

        Assert.Equal(SurveyRespondentText.Notice, json["notice"]!.GetValue<string>());
        Assert.DoesNotContain(answers, answer => answer.Contains("evil.example", StringComparison.Ordinal));
        Assert.DoesNotContain(answers, answer => answer.Contains("<b>", StringComparison.Ordinal));
        Assert.Equal(SurveyRespondentText.Clean("Great course, see https://evil.example/claim for a prize"), answers[0]);
    }
}
