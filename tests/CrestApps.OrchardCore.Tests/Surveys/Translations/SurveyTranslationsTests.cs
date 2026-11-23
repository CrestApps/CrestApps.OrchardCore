using CrestApps.OrchardCore.Surveys.Core.Translations;

namespace CrestApps.OrchardCore.Tests.Surveys.Translations;

public sealed class SurveyTranslationsTests
{
    [Fact]
    public void Merge_DropsKeysOfMissingItemsAndRemovesEmptyTexts()
    {
        // Arrange
        var definition = TranslationsDefinitions.Create();
        definition.Translations["fr"] = new Dictionary<string, string> { [SurveyTextKeys.QuestionTitle("q2")] = "Notez-nous" };

        // Act
        var written = SurveyTranslations.Merge(definition, "fr",
        [
            new(SurveyTextKeys.QuestionTitle("q1"), " Content ? "),
            new(SurveyTextKeys.QuestionTitle("removed"), "Ignoré"),
            new(SurveyTextKeys.QuestionTitle("q2"), ""),
        ]);

        // Assert
        Assert.Equal(2, written);
        Assert.Equal("Content ?", definition.Translations["fr"][SurveyTextKeys.QuestionTitle("q1")]);
        Assert.False(definition.Translations["fr"].ContainsKey(SurveyTextKeys.QuestionTitle("q2")));
        Assert.False(definition.Translations["fr"].ContainsKey(SurveyTextKeys.QuestionTitle("removed")));
    }

    [Fact]
    public void Merge_WithoutOverwrite_FillsOnlyMissingTexts()
    {
        // Arrange
        var definition = TranslationsDefinitions.Create();
        definition.Translations["fr"] = new Dictionary<string, string> { [SurveyTextKeys.SurveyTitle] = "Enquête" };

        // Act
        SurveyTranslations.Merge(definition, "FR",
        [
            new(SurveyTextKeys.SurveyTitle, "Autre"),
            new(SurveyTextKeys.PageTitle("p1"), "À propos de vous"),
        ], overwrite: false);

        // Assert
        Assert.Single(definition.Translations);
        Assert.Equal("Enquête", definition.Translations["fr"][SurveyTextKeys.SurveyTitle]);
        Assert.Equal("À propos de vous", definition.Translations["fr"][SurveyTextKeys.PageTitle("p1")]);
    }

    [Fact]
    public void Merge_LeavingACultureEmpty_RemovesTheCulture()
    {
        // Arrange
        var definition = TranslationsDefinitions.Create();
        definition.Translations["fr"] = new Dictionary<string, string> { [SurveyTextKeys.SurveyTitle] = "Enquête" };

        // Act
        SurveyTranslations.Merge(definition, "fr", [new(SurveyTextKeys.SurveyTitle, " ")]);

        // Assert
        Assert.Empty(definition.Translations);
    }

    [Fact]
    public void Export_ListsEveryTextWithItsTranslation()
    {
        // Arrange
        var definition = TranslationsDefinitions.Create();
        definition.Translations["fr"] = new Dictionary<string, string> { [SurveyTextKeys.Choice("q1", "c1")] = "Oui" };

        // Act
        var file = SurveyTranslations.Export("customer", definition, "fr");

        // Assert
        Assert.Equal("customer", file.Survey);
        Assert.Contains(file.Texts, entry => entry.Key == SurveyTextKeys.Choice("q1", "c1") && entry.Original == "Yes" && entry.Translation == "Oui");
        Assert.Contains(file.Texts, entry => entry.Key == SurveyTextKeys.QuestionPlaceholder("q1") && entry.Translation is null);
        Assert.DoesNotContain(file.Texts, entry => entry.Key == SurveyTextKeys.QuestionDescription("q1"));
    }

    [Fact]
    public void TextKeys_MatchTheKeysQuestionTypesAndTheProjectorUse()
    {
        // The projector and the choice question types read choice texts as "choice:{question}:{choice}" and titles as
        // "title:{question}", so a translation written under these keys reaches them.
        Assert.Equal("choice:q1:c1", SurveyTextKeys.Choice("q1", "c1"));
        Assert.Equal("title:q1", SurveyTextKeys.QuestionTitle("q1"));
        Assert.Equal("row:q2:r1", SurveyTextKeys.Row("q2", "r1"));
    }

    [Fact]
    public void OutdatedKeys_AfterTheOriginalChanges_ListsTheTranslationUntilItIsTranslatedAgain()
    {
        // Arrange
        var definition = TranslationsDefinitions.Create();
        var key = SurveyTextKeys.QuestionTitle("q1");
        SurveyTranslations.Merge(definition, "fr", [new(key, "Êtes-vous satisfait ?")]);
        Assert.Empty(SurveyTranslations.OutdatedKeys(definition, "fr"));

        // Act: the author rewords the question, then the page form posts the old translation again unchanged.
        definition.Pages[0].Questions[0].Title = "Are you unhappy?";
        var outdated = SurveyTranslations.OutdatedKeys(definition, "fr");
        SurveyTranslations.Merge(definition, "fr", [new(key, "Êtes-vous satisfait ?")]);
        var stillOutdated = SurveyTranslations.OutdatedKeys(definition, "fr");
        SurveyTranslations.Merge(definition, "fr", [new(key, "Êtes-vous mécontent ?")]);

        // Assert
        Assert.Equal([key], outdated);
        Assert.Equal([key], stillOutdated);
        Assert.Empty(SurveyTranslations.OutdatedKeys(definition, "fr"));
    }

    [Fact]
    public void OutdatedKeys_OfATranslationWithoutAFingerprint_IsEmpty()
    {
        // Arrange: a translation saved before fingerprints were kept.
        var definition = TranslationsDefinitions.Create();
        definition.Translations["fr"] = new Dictionary<string, string> { [SurveyTextKeys.QuestionTitle("q1")] = "Content ?" };

        // Act
        var outdated = SurveyTranslations.OutdatedKeys(definition, "fr");

        // Assert
        Assert.Empty(outdated);
    }

    [Fact]
    public void SplitByOriginal_LeavesOutEntriesTranslatedFromAnOlderText()
    {
        // Arrange
        var definition = TranslationsDefinitions.Create();
        var file = SurveyTranslations.Export("customer", definition, "fr");
        file.Texts.Single(entry => entry.Key == SurveyTextKeys.QuestionTitle("q1")).Translation = "Êtes-vous satisfait ?";
        file.Texts.Single(entry => entry.Key == SurveyTextKeys.PageTitle("p1")).Translation = "À propos de vous";
        definition.Pages[0].Questions[0].Title = "Are you unhappy?";

        // Act
        var (current, changed) = SurveyTranslations.SplitByOriginal(definition, file.Texts);

        // Assert
        Assert.Equal(1, changed);
        Assert.DoesNotContain(current, entry => entry.Key == SurveyTextKeys.QuestionTitle("q1"));
        Assert.Contains(current, entry => entry.Key == SurveyTextKeys.PageTitle("p1"));
    }
}
