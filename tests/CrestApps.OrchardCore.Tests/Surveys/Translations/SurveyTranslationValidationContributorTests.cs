using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Core.Translations;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;

namespace CrestApps.OrchardCore.Tests.Surveys.Translations;

public sealed class SurveyTranslationValidationContributorTests
{
    [Fact]
    public async Task ValidateAsync_WithTextsOfRemovedItems_AddsAWarningAndNoError()
    {
        // Arrange
        var definition = TranslationsDefinitions.Create();
        definition.Translations["fr"] = new Dictionary<string, string>
        {
            [SurveyTextKeys.SurveyTitle] = "Enquête",
            [SurveyTextKeys.QuestionTitle("gone")] = "Supprimé",
        };

        var context = new SurveyDefinitionValidationContext(null, definition);

        // Act
        await new SurveyTranslationValidationContributor(new AuthoringLocalizer<SurveyTranslationValidationContributor>()).ValidateAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(context.Succeeded);
        Assert.Single(context.Messages, message => message.Code == "stale-translation");
    }

    [Fact]
    public async Task ValidateAsync_WithATranslationOfAChangedOriginal_WarnsItIsOutOfDate()
    {
        // Arrange
        var definition = TranslationsDefinitions.Create();
        SurveyTranslations.Merge(definition, "fr", [new(SurveyTextKeys.QuestionTitle("q1"), "Êtes-vous satisfait ?")]);
        definition.Pages[0].Questions[0].Title = "Are you unhappy?";
        var context = new SurveyDefinitionValidationContext(null, definition);

        // Act
        await new SurveyTranslationValidationContributor(new AuthoringLocalizer<SurveyTranslationValidationContributor>()).ValidateAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(context.Succeeded);
        Assert.Single(context.Messages, message => message.Code == "outdated-translation");
    }
}
