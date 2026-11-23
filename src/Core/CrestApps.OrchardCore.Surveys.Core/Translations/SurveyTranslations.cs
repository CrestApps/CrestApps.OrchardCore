using System.Security.Cryptography;
using System.Text;
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
    /// Gets the fingerprint of an original text, stored with each translation so a later change of the original shows.
    /// </summary>
    /// <param name="text">The original text.</param>
    /// <returns>The fingerprint, or <see langword="null"/> for an empty text.</returns>
    public static string Fingerprint(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.Trim())), 0, 8);
    }

    /// <summary>
    /// Gets the keys of a culture whose translation was made from an original text that has changed since. A translation
    /// saved before fingerprints were kept has nothing to compare with and is not counted.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <param name="culture">The culture.</param>
    /// <returns>The keys.</returns>
    public static IReadOnlySet<string> OutdatedKeys(SurveyDefinition definition, string culture)
    {
        var outdated = new HashSet<string>(StringComparer.Ordinal);
        var texts = Texts(definition, culture);
        var sources = Sources(definition, culture);

        if (texts.Count == 0 || sources is null || sources.Count == 0)
        {
            return outdated;
        }

        foreach (var entry in SurveyTextCatalog.Describe(definition))
        {
            if (texts.TryGetValue(entry.Key, out var text)
                && !string.IsNullOrWhiteSpace(text)
                && sources.TryGetValue(entry.Key, out var fingerprint)
                && !string.IsNullOrEmpty(fingerprint)
                && !string.Equals(fingerprint, Fingerprint(entry.Text), StringComparison.Ordinal))
            {
                outdated.Add(entry.Key);
            }
        }

        return outdated;
    }

    /// <summary>
    /// Gets the keys of a culture whose translation has no fingerprint of its original text (saved before fingerprints
    /// were kept), so nothing tells whether the original changed since. They are shown to be checked once: saving the
    /// translation again, changed or not, records the original it now stands for.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <param name="culture">The culture.</param>
    /// <returns>The keys.</returns>
    public static IReadOnlySet<string> UncheckedKeys(SurveyDefinition definition, string culture)
    {
        var toCheck = new HashSet<string>(StringComparer.Ordinal);
        var texts = Texts(definition, culture);

        if (texts.Count == 0)
        {
            return toCheck;
        }

        var sources = Sources(definition, culture);

        foreach (var entry in SurveyTextCatalog.Describe(definition))
        {
            if (texts.TryGetValue(entry.Key, out var text)
                && !string.IsNullOrWhiteSpace(text)
                && !string.IsNullOrWhiteSpace(entry.Text)
                && (sources is null || !sources.TryGetValue(entry.Key, out var fingerprint) || string.IsNullOrEmpty(fingerprint)))
            {
                toCheck.Add(entry.Key);
            }
        }

        return toCheck;
    }

    /// <summary>
    /// Splits the texts of a translation file into those made from the current original texts and those whose original
    /// text in the file differs from the survey's now (the file was translated before an author reworded the item). An
    /// entry without an original is taken as current.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <param name="entries">The entries of the file.</param>
    /// <returns>The current entries, and how many were left out.</returns>
    public static (IReadOnlyList<SurveyTranslationFileEntry> Current, int Changed) SplitByOriginal(SurveyDefinition definition, IEnumerable<SurveyTranslationFileEntry> entries)
    {
        var originals = SurveyTextCatalog.Describe(definition).ToDictionary(entry => entry.Key, entry => entry.Text, StringComparer.Ordinal);
        var current = new List<SurveyTranslationFileEntry>();
        var changed = 0;

        foreach (var entry in entries ?? [])
        {
            if (string.IsNullOrEmpty(entry?.Key))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(entry.Original)
                && originals.TryGetValue(entry.Key, out var original)
                && !string.Equals(entry.Original.Trim(), original?.Trim(), StringComparison.Ordinal))
            {
                changed++;

                continue;
            }

            current.Add(entry);
        }

        return (current, changed);
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

        definition.TranslationSources ??= [];

        if (!definition.TranslationSources.TryGetValue(name, out var sources) || sources is null)
        {
            sources = new Dictionary<string, string>(StringComparer.Ordinal);
            definition.TranslationSources[name] = sources;
        }

        var known = SurveyTextCatalog.KnownKeys(definition);
        var originals = SurveyTextCatalog.Describe(definition).ToDictionary(entry => entry.Key, entry => entry.Text, StringComparer.Ordinal);
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
                    sources.Remove(key);
                    written++;
                }

                continue;
            }

            if (!overwrite && target.TryGetValue(key, out var existing) && !string.IsNullOrWhiteSpace(existing))
            {
                continue;
            }

            var trimmed = text.Trim();

            // A translation posted again unchanged keeps the fingerprint it was made from, so it stays marked out of
            // date until someone really translates the new original.
            if (target.TryGetValue(key, out var previous) && string.Equals(previous, trimmed, StringComparison.Ordinal))
            {
                // One saved before fingerprints were kept is checked by being saved again: it now stands for the
                // current original.
                if (!sources.ContainsKey(key) && Fingerprint(originals.GetValueOrDefault(key)) is { } checkedFingerprint)
                {
                    sources[key] = checkedFingerprint;
                    written++;
                }

                continue;
            }

            target[key] = trimmed;

            if (Fingerprint(originals.GetValueOrDefault(key)) is { } fingerprint)
            {
                sources[key] = fingerprint;
            }

            written++;
        }

        foreach (var key in sources.Keys.Where(key => !target.ContainsKey(key)).ToList())
        {
            sources.Remove(key);
        }

        if (target.Count == 0)
        {
            definition.Translations.Remove(name);
        }

        if (sources.Count == 0)
        {
            definition.TranslationSources.Remove(name);
        }

        return written;
    }

    private static Dictionary<string, string> Sources(SurveyDefinition definition, string culture)
    {
        if (definition?.TranslationSources is null || string.IsNullOrWhiteSpace(culture))
        {
            return null;
        }

        var match = definition.TranslationSources.Keys.FirstOrDefault(key => string.Equals(key, culture.Trim(), StringComparison.OrdinalIgnoreCase));

        return match is null ? null : definition.TranslationSources[match];
    }
}
