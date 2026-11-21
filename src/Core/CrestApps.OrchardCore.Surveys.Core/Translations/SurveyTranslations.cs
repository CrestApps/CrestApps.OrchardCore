using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Translations;

/// <summary>
/// Reads and writes the translations of a definition: building and reading translation files, and merging texts.
/// </summary>
public static class SurveyTranslations
{
    /// <summary>
    /// Builds the translation file of a culture: every translatable text with its current translation.
    /// </summary>
    /// <param name="surveyName">The technical name of the survey.</param>
    /// <param name="definition">The definition.</param>
    /// <param name="culture">The culture.</param>
    /// <returns>The file.</returns>
    public static SurveyTranslationFile Export(string surveyName, SurveyDefinition definition, string culture)
    {
        var texts = Texts(definition, culture);

        return new SurveyTranslationFile
        {
            Survey = surveyName,
            Culture = culture,
            Texts = SurveyTextCatalog.Describe(definition)
                .Select(entry => new SurveyTranslationFileEntry
                {
                    Key = entry.Key,
                    Original = entry.Text,
                    Translation = texts.TryGetValue(entry.Key, out var text) ? text : null,
                })
                .ToList(),
        };
    }

    /// <summary>
    /// Gets the translated texts of a culture, or an empty dictionary.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <param name="culture">The culture.</param>
    /// <returns>The texts by key.</returns>
    public static IReadOnlyDictionary<string, string> Texts(SurveyDefinition definition, string culture)
    {
        if (definition?.Translations is null || string.IsNullOrWhiteSpace(culture))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var match = definition.Translations.Keys.FirstOrDefault(key => string.Equals(key, culture.Trim(), StringComparison.OrdinalIgnoreCase));

        return match is not null && definition.Translations[match] is { } texts
            ? texts
            : new Dictionary<string, string>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Writes texts of a culture into a definition. Keys of items that do not exist are left out; an empty text removes
    /// the translation of its key. A culture left without texts is removed.
    /// </summary>
    /// <param name="definition">The definition to change.</param>
    /// <param name="culture">The culture.</param>
    /// <param name="texts">The texts by key.</param>
    /// <param name="overwrite">Whether a text replaces an existing translation; when <see langword="false"/>, only missing translations are filled.</param>
    /// <returns>How many texts were written.</returns>
    public static int Merge(SurveyDefinition definition, string culture, IEnumerable<KeyValuePair<string, string>> texts, bool overwrite = true)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(culture);

        definition.Translations ??= [];

        var name = definition.Translations.Keys.FirstOrDefault(key => string.Equals(key, culture.Trim(), StringComparison.OrdinalIgnoreCase)) ?? culture.Trim();

        if (!definition.Translations.TryGetValue(name, out var target) || target is null)
        {
            target = new Dictionary<string, string>(StringComparer.Ordinal);
            definition.Translations[name] = target;
        }

        var known = SurveyTextCatalog.KnownKeys(definition);
        var written = 0;

        foreach (var (key, text) in texts ?? [])
        {
            if (string.IsNullOrEmpty(key) || !known.Contains(key))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                if (overwrite && target.Remove(key))
                {
                    written++;
                }

                continue;
            }

            if (!overwrite && target.TryGetValue(key, out var existing) && !string.IsNullOrWhiteSpace(existing))
            {
                continue;
            }

            target[key] = text.Trim();
            written++;
        }

        if (target.Count == 0)
        {
            definition.Translations.Remove(name);
        }

        return written;
    }
}
