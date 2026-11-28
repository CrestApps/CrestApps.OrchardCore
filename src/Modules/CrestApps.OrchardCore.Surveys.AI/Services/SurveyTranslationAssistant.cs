using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Core.Translations;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Infrastructure.Html;

namespace CrestApps.OrchardCore.Surveys.AI.Services;

/// <summary>
/// Translates the texts of a survey's draft into a culture with AI and writes them into its translations, for an author
/// to review on the translations page before publishing. The model can take minutes, so the survey is read again before
/// the translations are saved, and they are saved only when nobody changed the draft in the meantime.
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
    /// <returns>How many texts were written, or why nothing was saved.</returns>
    public async Task<SurveyTranslationResult> TranslateAsync(ClaimsPrincipal user, string surveyId, string culture, bool overwrite, CancellationToken cancellationToken)
    {
        var survey = string.IsNullOrEmpty(surveyId) ? null : await _surveys.FindByIdAsync(surveyId, cancellationToken);

        if (survey is null || string.IsNullOrWhiteSpace(culture) || !await _authorization.AuthorizeAsync(user, survey, SurveyOperation.Manage, cancellationToken))
        {
            return new SurveyTranslationResult { Error = SurveyErrorCode.NotFound };
        }

        // The draft the model translates: the translations are saved only against this version of it.
        var stamp = survey.DraftModifiedUtc;
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
                return new SurveyTranslationResult { Error = SurveyErrorCode.NotAvailableNow };
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

        if (translated.Count == 0)
        {
            return new SurveyTranslationResult();
        }

        return await SaveAsync(surveyId, stamp, culture, translated, overwrite, cancellationToken);
    }

    // Reads the survey again (in a child scope inside a shell, so the read sees what other requests saved while the model
    // ran rather than this scope's copy), merges the translations into its current draft and saves them with the stamp
    // of the draft that was translated: a co-author's edit, a publish or an access change made meanwhile is never
    // overwritten, the caller is told to try again.
    private async Task<SurveyTranslationResult> SaveAsync(string surveyId, DateTime? stamp, string culture, Dictionary<string, string> translated, bool overwrite, CancellationToken cancellationToken)
    {
        if (ShellScope.Current is null)
        {
            return await SaveAsync(_surveys, surveyId, stamp, culture, translated, overwrite, cancellationToken);
        }

        SurveyTranslationResult result = null;

        await ShellScope.UsingChildScopeAsync(async scope =>
            result = await SaveAsync(scope.ServiceProvider.GetRequiredService<ISurveyManager>(), surveyId, stamp, culture, translated, overwrite, cancellationToken));

        return result;
    }

    private static async Task<SurveyTranslationResult> SaveAsync(ISurveyManager surveys, string surveyId, DateTime? stamp, string culture, Dictionary<string, string> translated, bool overwrite, CancellationToken cancellationToken)
    {
        var current = await surveys.FindByIdAsync(surveyId, cancellationToken);

        if (current is null)
        {
            return new SurveyTranslationResult { Error = SurveyErrorCode.NotFound };
        }

        // A draft never saved has no stamp, and the manager has nothing to compare it with.
        if (stamp is null && current.DraftModifiedUtc is not null)
        {
            return new SurveyTranslationResult { Error = SurveyErrorCode.Busy };
        }

        var draft = SurveyDefinitionCloner.Clone(current.Draft) ?? new SurveyDefinition();
        var written = SurveyTranslations.Merge(draft, culture, translated, overwrite);

        if (written == 0)
        {
            return new SurveyTranslationResult();
        }

        var error = await surveys.SaveDraftAsync(current, draft, stamp, cancellationToken);

        return new SurveyTranslationResult { Error = error, Written = error == SurveyErrorCode.None ? written : 0 };
    }
}
