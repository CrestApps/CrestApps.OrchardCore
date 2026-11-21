using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.CompilerServices;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Translations;

/// <summary>
/// Gives the copy of a survey definition whose texts are in a culture, from its <see cref="SurveyDefinition.Translations"/>.
/// A text without a translation keeps its original. Only texts change: ids, logic and keys are the same, so the copy
/// is only used to present the survey, never to score or export it. Copies are cached per definition instance and
/// culture; a published version never changes, so its copies stay valid.
/// </summary>
public sealed class SurveyDefinitionLocalizer
{
    private readonly ConditionalWeakTable<SurveyDefinition, ConcurrentDictionary<string, SurveyDefinition>> _cache = new();
    private readonly ConditionalWeakTable<SurveyDefinition, object> _localized = new();

    /// <summary>
    /// Finds the culture of a definition's translations that serves a culture: the culture itself, else its parent
    /// (fr for fr-CA).
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <param name="culture">The culture, or <see langword="null"/>.</param>
    /// <returns>The translation culture, or <see langword="null"/> when there is none.</returns>
    public static string ResolveCulture(SurveyDefinition definition, string culture)
    {
        if (definition?.Translations is not { Count: > 0 } translations || string.IsNullOrWhiteSpace(culture))
        {
            return null;
        }

        var name = culture.Trim();

        for (var attempt = 0; attempt < 4 && !string.IsNullOrEmpty(name); attempt++)
        {
            var match = translations.Keys.FirstOrDefault(key => string.Equals(key, name, StringComparison.OrdinalIgnoreCase));

            if (match is not null && translations[match] is { Count: > 0 })
            {
                return match;
            }

            name = Parent(name);
        }

        return null;
    }

    /// <summary>
    /// Gets a text of a definition in a culture.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <param name="culture">The culture, or <see langword="null"/>.</param>
    /// <param name="key">The text key.</param>
    /// <param name="original">The original text.</param>
    /// <returns>The translation, or the original text.</returns>
    public static string Translate(SurveyDefinition definition, string culture, string key, string original)
    {
        var resolved = ResolveCulture(definition, culture);

        return resolved is not null
            && definition.Translations[resolved].TryGetValue(key ?? string.Empty, out var text)
            && !string.IsNullOrWhiteSpace(text)
            ? text
            : original;
    }

    /// <summary>
    /// Gets the copy of a definition in a culture, or the definition itself when it has no translation for it.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <param name="culture">The culture, or <see langword="null"/>.</param>
    /// <returns>The localized definition.</returns>
    public SurveyDefinition Localize(SurveyDefinition definition, string culture)
    {
        if (definition is null || _localized.TryGetValue(definition, out _))
        {
            return definition;
        }

        var resolved = ResolveCulture(definition, culture);

        if (resolved is null)
        {
            return definition;
        }

        var copies = _cache.GetOrCreateValue(definition);

        return copies.GetOrAdd(resolved, name =>
        {
            var copy = Apply(definition, definition.Translations[name]);
            _localized.AddOrUpdate(copy, null);

            return copy;
        });
    }

    /// <summary>
    /// Makes a copy of a definition with translated texts.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <param name="texts">The translated texts by key.</param>
    /// <returns>The copy.</returns>
    public static SurveyDefinition Apply(SurveyDefinition definition, IReadOnlyDictionary<string, string> texts)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var copy = SurveyDefinitionCloner.Clone(definition);

        if (texts is null || texts.Count == 0)
        {
            return copy;
        }

        string Text(string key, string original)
            => texts.TryGetValue(key, out var text) && !string.IsNullOrWhiteSpace(text) ? text : original;

        copy.Title = Text(SurveyTextKeys.SurveyTitle, copy.Title);
        copy.Introduction = Text(SurveyTextKeys.SurveyIntroduction, copy.Introduction);

        foreach (var page in copy.Pages ?? [])
        {
            if (string.IsNullOrEmpty(page?.Id))
            {
                continue;
            }

            page.Title = Text(SurveyTextKeys.PageTitle(page.Id), page.Title);
            page.Description = Text(SurveyTextKeys.PageDescription(page.Id), page.Description);

            foreach (var question in page.Questions ?? [])
            {
                if (string.IsNullOrEmpty(question?.Id))
                {
                    continue;
                }

                question.Title = Text(SurveyTextKeys.QuestionTitle(question.Id), question.Title);
                question.Description = Text(SurveyTextKeys.QuestionDescription(question.Id), question.Description);
                question.Placeholder = Text(SurveyTextKeys.QuestionPlaceholder(question.Id), question.Placeholder);

                foreach (var choice in question.Choices ?? [])
                {
                    if (!string.IsNullOrEmpty(choice?.Id))
                    {
                        choice.Text = Text(SurveyTextKeys.Choice(question.Id, choice.Id), choice.Text);
                    }
                }

                foreach (var row in question.Rows ?? [])
                {
                    if (!string.IsNullOrEmpty(row?.Id))
                    {
                        row.Text = Text(SurveyTextKeys.Row(question.Id, row.Id), row.Text);
                    }
                }
            }
        }

        foreach (var ending in copy.Endings ?? [])
        {
            if (string.IsNullOrEmpty(ending?.Id))
            {
                continue;
            }

            ending.Title = Text(SurveyTextKeys.EndingTitle(ending.Id), ending.Title);
            ending.Message = Text(SurveyTextKeys.EndingMessage(ending.Id), ending.Message);
        }

        return copy;
    }

    private static string Parent(string culture)
    {
        try
        {
            var parent = CultureInfo.GetCultureInfo(culture).Parent;

            return parent is null || string.IsNullOrEmpty(parent.Name) ? null : parent.Name;
        }
        catch (CultureNotFoundException)
        {
            var dash = culture.LastIndexOf('-');

            return dash > 0 ? culture.Substring(0, dash) : null;
        }
    }
}
