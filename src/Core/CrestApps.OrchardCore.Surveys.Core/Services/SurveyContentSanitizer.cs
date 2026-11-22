using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Surveys.Core.Translations;
using CrestApps.OrchardCore.Surveys.Models;
using OrchardCore.Infrastructure.Html;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Sanitizes the author texts of a survey that are shown as HTML: the introduction, page descriptions, question titles
/// and descriptions, feedback, text blocks and ending messages, with their translations. The survey, version, template
/// and bank question stores run it on every save, so every write path (editors, recipes, templates, banks, QTI,
/// spreadsheets, Moodle and AI drafts) goes through it. The views sanitize again when they show the text, which covers
/// content stored before this ran.
/// </summary>
/// <remarks>
/// Texts shown encoded (survey, page and ending titles, choices, rows, placeholders) are left as written. An ending
/// message that holds Liquid is not sanitized on save, because the sanitizer would encode its operators; its rendered
/// output is sanitized instead.
/// </remarks>
public sealed class SurveyContentSanitizer
{
    private readonly IHtmlSanitizerService _sanitizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyContentSanitizer"/> class.
    /// </summary>
    /// <param name="sanitizer">The HTML sanitizer.</param>
    public SurveyContentSanitizer(IHtmlSanitizerService sanitizer)
    {
        ArgumentNullException.ThrowIfNull(sanitizer);

        _sanitizer = sanitizer;
    }

    /// <summary>
    /// Sanitizes an HTML text. A text without markup is returned as it is.
    /// </summary>
    /// <param name="html">The text.</param>
    /// <returns>The sanitized text, or an empty string for an empty text.</returns>
    public string Sanitize(string html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return string.Empty;
        }

        // Script needs markup; a text without a tag cannot carry any, and is kept as written.
        return html.Contains('<', StringComparison.Ordinal) ? _sanitizer.Sanitize(html) : html;
    }

    /// <summary>
    /// Sanitizes the HTML texts of a definition in place.
    /// </summary>
    /// <param name="definition">The definition, or <see langword="null"/>.</param>
    public void Sanitize(SurveyDefinition definition)
    {
        if (definition is null)
        {
            return;
        }

        definition.Introduction = Keep(definition.Introduction);

        foreach (var page in definition.Pages ?? [])
        {
            if (page is null)
            {
                continue;
            }

            page.Description = Keep(page.Description);

            foreach (var question in page.Questions ?? [])
            {
                Sanitize(question);
            }
        }

        foreach (var ending in definition.Endings ?? [])
        {
            if (ending is not null && !HasLiquid(ending.Message))
            {
                ending.Message = Keep(ending.Message);
            }
        }

        if (definition.Translations is null)
        {
            return;
        }

        var html = SurveyTextCatalog.Describe(definition)
            .Where(entry => entry.IsHtml)
            .Select(entry => entry.Key)
            .Concat((definition.Pages ?? []).SelectMany(page => page?.Questions ?? []).Where(question => question is not null).Select(question => SurveyTextKeys.QuestionTitle(question.Id)))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var texts in definition.Translations.Values)
        {
            if (texts is null)
            {
                continue;
            }

            foreach (var key in texts.Keys.ToList())
            {
                if (html.Contains(key) && !(key.StartsWith("ending-message:", StringComparison.Ordinal) && HasLiquid(texts[key])))
                {
                    texts[key] = Keep(texts[key]);
                }
            }
        }
    }

    /// <summary>
    /// Sanitizes the HTML texts of a question in place: its title, description, feedback and text block.
    /// </summary>
    /// <param name="question">The question, or <see langword="null"/>.</param>
    public void Sanitize(SurveyQuestion question)
    {
        if (question is null)
        {
            return;
        }

        question.Title = Keep(question.Title);
        question.Description = Keep(question.Description);

        if (question.Scoring is { } scoring)
        {
            scoring.GeneralFeedback = Keep(scoring.GeneralFeedback);
            scoring.CorrectFeedback = Keep(scoring.CorrectFeedback);
            scoring.IncorrectFeedback = Keep(scoring.IncorrectFeedback);

            if (scoring.ChoiceFeedback is { Count: > 0 } choiceFeedback)
            {
                foreach (var key in choiceFeedback.Keys.ToList())
                {
                    choiceFeedback[key] = Keep(choiceFeedback[key]);
                }
            }
        }

        // Any question type keeps its author HTML in a settings property named Html (the text block does), so the rule
        // follows the property, not the type name, and custom types get it too.
        if (question.Settings is { } settings
            && settings.TryGetPropertyValue("Html", out var node)
            && node is JsonValue value
            && value.TryGetValue<string>(out var text))
        {
            settings["Html"] = Keep(text);
        }
    }

    private string Keep(string html)
        => string.IsNullOrEmpty(html) ? html : Sanitize(html);

    private static bool HasLiquid(string text)
        => !string.IsNullOrEmpty(text) && (text.Contains("{{", StringComparison.Ordinal) || text.Contains("{%", StringComparison.Ordinal));
}
