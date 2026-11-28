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
    /// Line breaks are normalized first, so a text that only went through a browser's text box (which sends CRLF) keeps
    /// its fingerprint.
    /// </summary>
    /// <param name="text">The original text.</param>
    /// <returns>The fingerprint, or <see langword="null"/> for an empty text.</returns>
    public static string Fingerprint(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return Hash(Normalize(text));
    }

    /// <summary>
    /// Normalizes a text for storing and comparing: CRLF and CR line breaks become LF, and the ends are trimmed.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The normalized text, or <see langword="null"/>.</returns>
    public static string Normalize(string text)
        => text?.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Trim();

    // Whether a stored fingerprint stands for a text: the normalized fingerprint, or the one of the text as it was hashed
    // before line breaks were normalized, so a fingerprint stored then is not read as a changed original.
    private static bool Fingerprints(string fingerprint, string text)
        => string.Equals(fingerprint, Fingerprint(text), StringComparison.Ordinal)
            || (!string.IsNullOrWhiteSpace(text) && string.Equals(fingerprint, Hash(text.Trim()), StringComparison.Ordinal));

    private static string Hash(string text)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)), 0, 8);

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
                && !Fingerprints(fingerprint, entry.Text))
            {
                outdated.Add(entry.Key);
            }
        }

        return outdated;
    }

    /// <summary>
    /// Gets the keys of a culture whose translation has no fingerprint of its original text (saved before fingerprints
    /// were kept), so nothing tells whether the original changed since. They are shown to be checked once: changing the
    /// translation, or confirming it as it is, records the original it now stands for.
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
                && !string.Equals(Normalize(entry.Original), Normalize(original), StringComparison.Ordinal))
            {
                changed++;

                continue;
            }

            current.Add(entry);
        }

        return (current, changed);
    }

    /// <summary>
    /// Imports the entries of a translation file into a definition. An entry whose original in the file differs from the
    /// survey's now (translated before an author reworded the item) is left out and counted. The others are written; only
    /// a translation the file changes records the current original. A translation the file gives back unchanged is not
    /// confirmed, even next to the current original: the export writes the current original next to every translation,
    /// out of date ones included, so a translator who only adds new texts would otherwise mark every stale one current.
    /// </summary>
    /// <param name="definition">The definition to change.</param>
    /// <param name="culture">The culture.</param>
    /// <param name="entries">The entries of the file.</param>
    /// <param name="sanitizeHtml">Cleans the translation of a rich text, or <see langword="null"/> to keep it as it is.</param>
    /// <returns>How many texts were written, and how many were left out because their original changed.</returns>
    public static (int Written, int Changed) Import(SurveyDefinition definition, string culture, IEnumerable<SurveyTranslationFileEntry> entries, Func<string, string> sanitizeHtml = null)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var html = SurveyTextCatalog.Describe(definition).Where(entry => entry.IsHtml).Select(entry => entry.Key).ToHashSet(StringComparer.Ordinal);
        var (current, changed) = SplitByOriginal(definition, entries);
        var written = Merge(definition, culture, current
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Translation))
            .Select(entry => new KeyValuePair<string, string>(entry.Key, html.Contains(entry.Key) && sanitizeHtml is not null ? sanitizeHtml(entry.Translation) : entry.Translation)), overwrite: true);

        return (written, changed);
    }

    /// <summary>
    /// Writes texts of a culture into a definition. Keys of items that do not exist are left out; an empty text removes
    /// the translation of its key. A culture left without texts is removed. A changed text records the original it was
    /// made from; a text posted again unchanged (line breaks normalized) changes nothing, unless its key is confirmed:
    /// the translator checked it against the current original, which it then stands for.
    /// </summary>
    /// <param name="definition">The definition to change.</param>
    /// <param name="culture">The culture.</param>
    /// <param name="texts">The texts by key.</param>
    /// <param name="overwrite">Whether a text replaces an existing translation; when <see langword="false"/>, only missing translations are filled.</param>
    /// <param name="confirmed">The keys whose unchanged translation the translator confirmed as current, or <see langword="null"/>.</param>
    /// <returns>How many texts were written or confirmed.</returns>
    public static int Merge(SurveyDefinition definition, string culture, IEnumerable<KeyValuePair<string, string>> texts, bool overwrite = true, IReadOnlySet<string> confirmed = null)
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

            var trimmed = Normalize(text);

            // A translation posted again unchanged keeps the fingerprint it was made from (or none), so it stays marked
            // out of date, or to be checked, until someone translates the new original or confirms it is current.
            if (target.TryGetValue(key, out var previous) && string.Equals(Normalize(previous), trimmed, StringComparison.Ordinal))
            {
                if (confirmed?.Contains(key) == true
                    && Fingerprint(originals.GetValueOrDefault(key)) is { } checkedFingerprint
                    && !string.Equals(sources.GetValueOrDefault(key), checkedFingerprint, StringComparison.Ordinal))
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
