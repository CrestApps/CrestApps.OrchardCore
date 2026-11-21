using CrestApps.OrchardCore.Surveys.Core.Translations;

namespace CrestApps.OrchardCore.Tests.Surveys.Translations;

public sealed class SurveyDefinitionLocalizerTests
{
    [Fact]
    public void Localize_WithATranslation_ReplacesTheTextsAndKeepsTheIds()
    {
        // Arrange
        var definition = TranslationsDefinitions.Create();
        definition.Translations["fr"] = new Dictionary<string, string>
        {
            [SurveyTextKeys.SurveyTitle] = "Enquête client",
            [SurveyTextKeys.QuestionTitle("q1")] = "Êtes-vous content ?",
            [SurveyTextKeys.Choice("q1", "c1")] = "Oui",
            [SurveyTextKeys.Row("q2", "r1")] = "Rapidité",
            [SurveyTextKeys.EndingMessage("e1")] = "<p>Merci.</p>",
        };

        // Act
        var localized = new SurveyDefinitionLocalizer().Localize(definition, "fr-CA");

        // Assert
        Assert.Equal("Enquête client", localized.Title);
        Assert.Equal("Êtes-vous content ?", localized.Pages[0].Questions[0].Title);
        Assert.Equal("Oui", localized.Pages[0].Questions[0].Choices[0].Text);
        Assert.Equal("No", localized.Pages[0].Questions[0].Choices[1].Text);
        Assert.Equal("Rapidité", localized.Pages[0].Questions[1].Rows[0].Text);
        Assert.Equal("<p>Merci.</p>", localized.Endings[0].Message);
        Assert.Equal("c1", localized.Pages[0].Questions[0].Choices[0].Id);
        Assert.Equal("Are you happy?", definition.Pages[0].Questions[0].Title);
    }

    [Fact]
    public void Localize_WithoutATranslation_ReturnsTheSameDefinition()
    {
        // Arrange
        var definition = TranslationsDefinitions.Create();

        // Act
        var localized = new SurveyDefinitionLocalizer().Localize(definition, "de");

        // Assert
        Assert.Same(definition, localized);
    }

    [Fact]
    public void Localize_CalledTwice_ReturnsTheCachedCopyAndDoesNotLocalizeACopyAgain()
    {
        // Arrange
        var localizer = new SurveyDefinitionLocalizer();
        var definition = TranslationsDefinitions.Create();
        definition.Translations["fr"] = new Dictionary<string, string> { [SurveyTextKeys.SurveyTitle] = "Enquête" };

        // Act
        var first = localizer.Localize(definition, "fr");
        var second = localizer.Localize(definition, "fr");
        var again = localizer.Localize(first, "fr");

        // Assert
        Assert.Same(first, second);
        Assert.Same(first, again);
    }

    [Theory]
    [InlineData("fr", "Enquête")]
    [InlineData("fr-BE", "Enquête")]
    [InlineData("es", "Customer survey")]
    [InlineData(null, "Customer survey")]
    public void Translate_ResolvesTheCultureOrItsParent(string culture, string expected)
    {
        // Arrange
        var definition = TranslationsDefinitions.Create();
        definition.Translations["fr"] = new Dictionary<string, string> { [SurveyTextKeys.SurveyTitle] = "Enquête" };

        // Act
        var text = SurveyDefinitionLocalizer.Translate(definition, culture, SurveyTextKeys.SurveyTitle, definition.Title);

        // Assert
        Assert.Equal(expected, text);
    }
}
