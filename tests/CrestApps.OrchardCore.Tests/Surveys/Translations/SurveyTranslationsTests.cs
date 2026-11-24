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
    public void R6L21_Merge_AMultiLineTranslationPostedWithCrlf_IsUnchanged_AndStaysOutOfDate()
    {
        // Arrange: a two-line translation saved, then the original reworded.
        var definition = TranslationsDefinitions.Create();
        var key = SurveyTextKeys.QuestionTitle("q1");
        SurveyTranslations.Merge(definition, "fr", [new(key, "Première ligne\nDeuxième ligne")]);
        definition.Pages[0].Questions[0].Title = "Are you unhappy?";

        // Act: the page's text box posts the same translation with CRLF line breaks.
        var written = SurveyTranslations.Merge(definition, "fr", [new(key, "Première ligne\r\nDeuxième ligne")]);

        // Assert
        Assert.Equal(0, written);
        Assert.Equal([key], SurveyTranslations.OutdatedKeys(definition, "fr"));
        Assert.Equal("Première ligne\nDeuxième ligne", SurveyTranslations.Texts(definition, "fr")[key]);
    }

    [Fact]
    public void R6L21_Fingerprint_IgnoresLineBreakStyle_AndAnOldCrlfFingerprintStillMatches()
    {
        // Arrange: an original with CRLF, fingerprinted before line breaks were normalized.
        var definition = TranslationsDefinitions.Create();
        var key = SurveyTextKeys.QuestionTitle("q1");
        definition.Pages[0].Questions[0].Title = "Line one\r\nLine two";
        definition.Translations["fr"] = new Dictionary<string, string> { [key] = "Ligne un\nLigne deux" };
        definition.TranslationSources["fr"] = new Dictionary<string, string>
        {
            [key] = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("Line one\r\nLine two")), 0, 8),
        };

        // Act
        var outdated = SurveyTranslations.OutdatedKeys(definition, "fr");

        // Assert
        Assert.Empty(outdated);
        Assert.Equal(SurveyTranslations.Fingerprint("Line one\nLine two"), SurveyTranslations.Fingerprint("Line one\r\nLine two"));
    }

    [Fact]
    public void R6L21_Merge_SavingThePage_LeavesUncheckedAndOutdatedEntries_UntilTheTranslatorConfirmsThem()
    {
        // Arrange: one translation saved before fingerprints were kept, one made before the original changed.
        var definition = TranslationsDefinitions.Create();
        var toCheck = SurveyTextKeys.QuestionTitle("q1");
        var stale = SurveyTextKeys.PageTitle("p1");
        SurveyTranslations.Merge(definition, "fr", [new(stale, "À propos de vous")]);
        definition.Translations["fr"][toCheck] = "Content ?";
        definition.Pages[0].Title = "About your visit";
        var page = new List<KeyValuePair<string, string>> { new(toCheck, "Content ?"), new(stale, "À propos de vous") };

        // Act: the page is saved unchanged, then saved with both confirmed.
        var saved = SurveyTranslations.Merge(definition, "fr", page);
        var uncheckedAfterSave = SurveyTranslations.UncheckedKeys(definition, "fr");
        var outdatedAfterSave = SurveyTranslations.OutdatedKeys(definition, "fr");
        var confirmed = SurveyTranslations.Merge(definition, "fr", page, overwrite: true, new HashSet<string>(StringComparer.Ordinal) { toCheck, stale });

        // Assert
        Assert.Equal(0, saved);
        Assert.Equal([toCheck], uncheckedAfterSave);
        Assert.Equal([stale], outdatedAfterSave);
        Assert.Equal(2, confirmed);
        Assert.Empty(SurveyTranslations.UncheckedKeys(definition, "fr"));
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
