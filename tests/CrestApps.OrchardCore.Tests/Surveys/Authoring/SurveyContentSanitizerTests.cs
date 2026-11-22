using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Infrastructure.Html;

namespace CrestApps.OrchardCore.Tests.Surveys.Authoring;

/// <summary>
/// The author texts shown as HTML lose their script on save, in the one place every write path goes through (the
/// stores), and when they are shown.
/// </summary>
public sealed class SurveyContentSanitizerTests
{
    private const string TitlePayload = "Your name? <img src=x onerror=\"document.title='XSS-TITLE'\">";
    private const string FeedbackPayload = "<p>Well done</p><script>alert('XSS-FEEDBACK')</script>";

    [Fact]
    public void Sanitize_TitleAndFeedbackPayloads_LoseTheirScript()
    {
        // Arrange
        var sanitizer = Sanitizer();
        var question = Question();

        // Act
        sanitizer.Sanitize(question);

        // Assert
        AssertSafe(question.Title);
        Assert.Contains("Your name?", question.Title, StringComparison.Ordinal);
        AssertSafe(question.Description);
        AssertSafe(question.Scoring.GeneralFeedback);
        AssertSafe(question.Scoring.CorrectFeedback);
        AssertSafe(question.Scoring.IncorrectFeedback);
        AssertSafe(question.Scoring.ChoiceFeedback["c1"]);
        Assert.Contains("<p>Well done</p>", question.Scoring.CorrectFeedback, StringComparison.Ordinal);
    }

    [Fact]
    public void Sanitize_Definition_CleansEveryHtmlText_AndLeavesPlainTextAndLiquidAlone()
    {
        // Arrange
        var sanitizer = Sanitizer();
        var block = new SurveyQuestion
        {
            Id = "tb",
            Name = "intro_block",
            ControlType = SurveyControlNames.TextBlock,
            Settings = new JsonObject { ["Html"] = FeedbackPayload },
        };
        var definition = new SurveyDefinition
        {
            Introduction = FeedbackPayload,
            Pages =
            [
                new SurveyPage { Id = "p1", Name = "p1", Title = "Rates & fees", Description = TitlePayload, Questions = [Question(), block] },
            ],
            Endings =
            [
                new SurveyEnding { Id = "e1", Name = "thanks", Message = FeedbackPayload },
                new SurveyEnding { Id = "e2", Name = "liquid", Message = "{% if Score > 5 %}Great{% endif %}" },
            ],
        };

        definition.Translations["fr"] = new Dictionary<string, string>
        {
            ["title:q1"] = TitlePayload,
            ["choice:q1:c1"] = "Tom & Jerry",
        };

        // Act
        sanitizer.Sanitize(definition);

        // Assert
        AssertSafe(definition.Introduction);
        AssertSafe(definition.Pages[0].Description);
        AssertSafe(definition.Pages[0].Questions[0].Title);
        AssertSafe(block.Settings["Html"]!.GetValue<string>());
        AssertSafe(definition.Endings[0].Message);
        AssertSafe(definition.Translations["fr"]["title:q1"]);

        // Texts shown encoded, and Liquid without markup, are kept as written.
        Assert.Equal("Rates & fees", definition.Pages[0].Title);
        Assert.Equal("Tom & Jerry", definition.Translations["fr"]["choice:q1:c1"]);
        Assert.Equal("{% if Score > 5 %}Great{% endif %}", definition.Endings[1].Message);
    }

    [Fact]
    public async Task SurveyStore_Save_StoresTheDraftSanitized()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync(services => services.AddScoped(_ => Sanitizer()));
        var definition = new SurveyDefinition { Pages = [new SurveyPage { Id = "p1", Name = "p1", Questions = [Question()] }] };

        // Act: the survey goes straight to the store, as a recipe does.
        var survey = await harness.RunAsync(async services =>
        {
            var created = new Survey { Name = "xss", Title = "XSS", Status = SurveyStatus.Draft, Draft = definition };

            await services.GetRequiredService<SurveyStore>().CreateAsync(created, TestContext.Current.CancellationToken);

            return created;
        });

        var stored = await harness.RunAsync(services => services.GetRequiredService<SurveyStore>().FindByIdAsync(survey.ItemId).AsTask());

        // Assert
        var question = stored.Draft.Pages[0].Questions[0];

        AssertSafe(question.Title);
        AssertSafe(question.Scoring.CorrectFeedback);
    }

    [Fact]
    public async Task BankQuestionStore_Save_StoresTheQuestionSanitized()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync(services => services.AddScoped(_ => Sanitizer()));

        // Act: a bank question saved the way the QTI, spreadsheet and Moodle imports save one.
        var saved = await harness.RunAsync(async services =>
        {
            var item = new BankQuestion { BankId = "bank1", Question = Question() };

            await services.GetRequiredService<BankQuestionStore>().CreateAsync(item, TestContext.Current.CancellationToken);

            return item;
        });

        var stored = await harness.RunAsync(services => services.GetRequiredService<BankQuestionStore>().FindByIdAsync(saved.ItemId).AsTask());

        // Assert
        AssertSafe(stored.Question.Title);
        AssertSafe(stored.Question.Scoring.IncorrectFeedback);
    }

    internal static SurveyContentSanitizer Sanitizer()
        => new(new HtmlSanitizerService(Options.Create(new HtmlSanitizerOptions())));

    internal static void AssertSafe(string html)
    {
        Assert.NotNull(html);
        Assert.DoesNotContain("onerror", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("XSS-", html, StringComparison.Ordinal);
    }

    private static SurveyQuestion Question()
        => new()
        {
            Id = "q1",
            Name = "q1",
            ControlType = SurveyControlNames.SingleChoice,
            Title = TitlePayload,
            Description = FeedbackPayload,
            Choices = [new SurveyChoice { Id = "c1", Value = "1", Text = "A & B" }],
            Scoring = new SurveyQuestionScoring
            {
                GeneralFeedback = FeedbackPayload,
                CorrectFeedback = FeedbackPayload,
                IncorrectFeedback = TitlePayload,
                ChoiceFeedback = new Dictionary<string, string> { ["c1"] = TitlePayload },
            },
        };
}
