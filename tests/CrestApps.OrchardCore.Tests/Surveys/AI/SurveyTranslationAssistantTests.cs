using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.AI.Services;
using CrestApps.OrchardCore.Surveys.Core.Translations;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tests.Surveys.Translations;
using Moq;
using OrchardCore.Infrastructure.Html;

namespace CrestApps.OrchardCore.Tests.Surveys.AI;

public sealed class SurveyTranslationAssistantTests
{
    [Fact]
    public async Task TranslateAsync_WritesTranslatedTextsOfExistingItemsIntoTheDraft()
    {
        // Arrange
        var survey = new Survey { ItemId = "s1", Name = "customer", Draft = TranslationsDefinitions.Create() };
        survey.Draft.Translations["fr"] = new Dictionary<string, string> { [SurveyTextKeys.SurveyTitle] = "Enquête existante" };

        SurveyDefinition saved = null;
        var surveys = new Mock<ISurveyManager>();
        surveys.Setup(manager => manager.FindByIdAsync("s1", It.IsAny<CancellationToken>())).ReturnsAsync(survey);
        surveys.Setup(manager => manager.SaveDraftAsync(survey, It.IsAny<SurveyDefinition>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Callback<Survey, SurveyDefinition, DateTime?, CancellationToken>((_, draft, _, _) => saved = draft)
            .ReturnsAsync(SurveyErrorCode.None);

        var authorization = new Mock<ISurveyAuthorizationService>();
        authorization.Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), survey, SurveyOperation.Manage, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // The fake model translates every text it is given by prefixing it, and adds a key that does not exist.
        var completer = new Mock<ISurveysAICompleter>();
        completer.Setup(service => service.CompleteAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, IDictionary<string, object> _, string input, CancellationToken _) =>
            {
                var output = new JsonObject { ["title:unknown"] = "Inconnu" };

                foreach (var (key, value) in JsonNode.Parse(input).AsObject())
                {
                    output[key] = "FR " + value.GetValue<string>();
                }

                return output.ToJsonString();
            });

        var sanitizer = new Mock<IHtmlSanitizerService>();
        sanitizer.Setup(service => service.Sanitize(It.IsAny<string>())).Returns<string>(html => html);

        var assistant = new SurveyTranslationAssistant(completer.Object, surveys.Object, authorization.Object, sanitizer.Object);

        // Act
        var result = await assistant.TranslateAsync(new ClaimsPrincipal(new ClaimsIdentity("Test")), "s1", "fr", overwrite: false, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Written > 0);
        Assert.Equal("Enquête existante", saved.Translations["fr"][SurveyTextKeys.SurveyTitle]);
        Assert.Equal("FR Yes", saved.Translations["fr"][SurveyTextKeys.Choice("q1", "c1")]);
        Assert.False(saved.Translations["fr"].ContainsKey("title:unknown"));
        Assert.False(survey.Draft.Translations["fr"].ContainsKey(SurveyTextKeys.Choice("q1", "c1")));
    }

    [Fact]
    public async Task TranslateAsync_WhenTheSurveyChangedWhileTheModelRan_SavesNothing()
    {
        // Arrange: round 12 M9. A co-author saves the draft while the model translates; the assistant reads the survey
        // again before it saves, and the manager refuses a save whose expected stamp is no longer the survey's.
        var loadedStamp = new DateTime(2026, 11, 27, 10, 0, 0, DateTimeKind.Utc);
        var loaded = new Survey { ItemId = "s1", Name = "customer", Draft = TranslationsDefinitions.Create(), DraftModifiedUtc = loadedStamp };
        var current = new Survey { ItemId = "s1", Name = "customer", Draft = TranslationsDefinitions.Create(), DraftModifiedUtc = loadedStamp.AddMinutes(2) };
        current.Draft.Title = "Changed by a co-author";
        var modelRan = false;

        SurveyDefinition saved = null;
        var surveys = new Mock<ISurveyManager>();
        surveys.Setup(manager => manager.FindByIdAsync("s1", It.IsAny<CancellationToken>())).ReturnsAsync(() => modelRan ? current : loaded);
        surveys.Setup(manager => manager.SaveDraftAsync(It.IsAny<Survey>(), It.IsAny<SurveyDefinition>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Survey survey, SurveyDefinition draft, DateTime? expected, CancellationToken _) =>
            {
                var stored = modelRan ? current : loaded;

                if (expected is { } stamp && stored.DraftModifiedUtc != stamp)
                {
                    return SurveyErrorCode.Busy;
                }

                saved = draft;

                return SurveyErrorCode.None;
            });

        var authorization = new Mock<ISurveyAuthorizationService>();
        authorization.Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<Survey>(), SurveyOperation.Manage, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var completer = new Mock<ISurveysAICompleter>();
        completer.Setup(service => service.CompleteAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, IDictionary<string, object> _, string input, CancellationToken _) =>
            {
                modelRan = true;
                var output = new JsonObject();

                foreach (var (key, value) in JsonNode.Parse(input).AsObject())
                {
                    output[key] = "FR " + value.GetValue<string>();
                }

                return output.ToJsonString();
            });

        var sanitizer = new Mock<IHtmlSanitizerService>();
        sanitizer.Setup(service => service.Sanitize(It.IsAny<string>())).Returns<string>(html => html);

        var assistant = new SurveyTranslationAssistant(completer.Object, surveys.Object, authorization.Object, sanitizer.Object);

        // Act
        var result = await assistant.TranslateAsync(new ClaimsPrincipal(new ClaimsIdentity("Test")), "s1", "fr", overwrite: false, TestContext.Current.CancellationToken);

        // Assert: nothing overwrote the co-author's draft, and the caller is told why.
        Assert.Null(saved);
        Assert.Equal(SurveyErrorCode.Busy, result.Error);
        Assert.Equal("Changed by a co-author", current.Draft.Title);
    }

    [Fact]
    public async Task TranslateAsync_WhenTheUserMayNotManageTheSurvey_ReturnsNotFound()
    {
        // Arrange
        var survey = new Survey { ItemId = "s1", Draft = TranslationsDefinitions.Create() };
        var surveys = new Mock<ISurveyManager>();
        surveys.Setup(manager => manager.FindByIdAsync("s1", It.IsAny<CancellationToken>())).ReturnsAsync(survey);

        var assistant = new SurveyTranslationAssistant(new Mock<ISurveysAICompleter>().Object, surveys.Object, new Mock<ISurveyAuthorizationService>().Object, new Mock<IHtmlSanitizerService>().Object);

        // Act
        var result = await assistant.TranslateAsync(new ClaimsPrincipal(), "s1", "fr", overwrite: true, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SurveyErrorCode.NotFound, result.Error);
    }
}
