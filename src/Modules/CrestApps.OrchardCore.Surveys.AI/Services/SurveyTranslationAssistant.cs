using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Core.Translations;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using OrchardCore.Infrastructure.Html;

namespace CrestApps.OrchardCore.Surveys.AI.Services;

/// <summary>
/// Translates the texts of a survey's draft into a culture with AI and writes them into its translations, for an author
/// to review on the translations page before publishing.
/// </summary>
public sealed class SurveyTranslationAssistant
{
    private const int BatchSize = 60;

    private readonly ISurveysAICompleter _completer;
    private readonly ISurveyManager _surveys;
    private readonly ISurveyAuthorizationService _authorization;
    private readonly IHtmlSanitizerService _htmlSanitizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyTranslationAssistant"/> class.
    /// </summary>
    /// <param name="completer">The AI completer.</param>
    /// <param name="surveys">The survey manager.</param>
    /// <param name="authorization">The survey authorization service.</param>
    /// <param name="htmlSanitizer">The HTML sanitizer.</param>
    public SurveyTranslationAssistant(
        ISurveysAICompleter completer,
        ISurveyManager surveys,
        ISurveyAuthorizationService authorization,
        IHtmlSanitizerService htmlSanitizer)
    {
        _completer = completer;
        _surveys = surveys;
        _authorization = authorization;
        _htmlSanitizer = htmlSanitizer;
    }

    /// <summary>
    /// Translates a survey's draft into a culture.
    /// </summary>
    /// <param name="user">The author.</param>
    /// <param name="surveyId">The survey.</param>
    /// <param name="culture">The culture.</param>
    /// <param name="overwrite">Whether existing translations are replaced; otherwise only missing ones are filled.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>How many texts were written, or <see langword="null"/> when the survey is not found, not allowed or no model is set up.</returns>
    public async Task<int?> TranslateAsync(ClaimsPrincipal user, string surveyId, string culture, bool overwrite, CancellationToken cancellationToken)
    {
        var survey = string.IsNullOrEmpty(surveyId) ? null : await _surveys.FindByIdAsync(surveyId, cancellationToken);

        if (survey is null || string.IsNullOrWhiteSpace(culture) || !await _authorization.AuthorizeAsync(user, survey, SurveyOperation.Manage, cancellationToken))
        {
            return null;
        }

        var draft = SurveyDefinitionCloner.Clone(survey.Draft) ?? new SurveyDefinition();
        var existing = SurveyTranslations.Texts(draft, culture);
        var entries = SurveyTextCatalog.Describe(draft)
            .Where(entry => overwrite || !existing.ContainsKey(entry.Key))
            .ToList();
        var html = entries.Where(entry => entry.IsHtml).Select(entry => entry.Key).ToHashSet(StringComparer.Ordinal);
        var language = SurveyAnswerSummarizer.LanguageName(culture);
        var translated = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var batch in entries.Chunk(BatchSize))
        {
            var input = new JsonObject();

            foreach (var entry in batch)
            {
                input[entry.Key] = entry.Text;
            }

            var output = await _completer.CompleteAsync(
                SurveysAIConstants.Templates.Translate,
                new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { ["Language"] = language },
                input.ToJsonString(),
                cancellationToken);

            if (output is null)
            {
                return null;
            }

            foreach (var (key, value) in SurveysAIJson.ReadObject(output) ?? [])
            {
                var text = SurveysAIJson.String(value, 10_000);

                if (text is not null && batch.Any(entry => entry.Key == key))
                {
                    translated[key] = html.Contains(key) ? _htmlSanitizer.Sanitize(text) : text;
                }
            }
        }

        var written = SurveyTranslations.Merge(draft, culture, translated, overwrite);

        if (written > 0)
        {
            await _surveys.SaveDraftAsync(survey, draft, cancellationToken: cancellationToken);
        }

        return written;
    }
}
