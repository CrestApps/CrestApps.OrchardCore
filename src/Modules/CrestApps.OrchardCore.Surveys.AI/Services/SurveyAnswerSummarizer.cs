using System.Globalization;
using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Surveys.AI.Services;

/// <summary>
/// Summarizes the latest written answers to one question into themes and sentiment, on demand.
/// </summary>
public sealed class SurveyAnswerSummarizer
{
    private readonly ISurveysAICompleter _completer;
    private readonly ISurveyManager _surveys;
    private readonly ISurveyVersionCache _versions;
    private readonly ISurveyAuthorizationService _authorization;
    private readonly ISurveyAnswerAggregates _aggregates;
    private readonly ISurveyControlRegistry _registry;
    private readonly ISiteService _siteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyAnswerSummarizer"/> class.
    /// </summary>
    /// <param name="completer">The AI completer.</param>
    /// <param name="surveys">The survey manager.</param>
    /// <param name="versions">The version cache.</param>
    /// <param name="authorization">The survey authorization service.</param>
    /// <param name="aggregates">The answer aggregates.</param>
    /// <param name="registry">The question type registry.</param>
    /// <param name="siteService">The site service, for the site's minimum group size floor.</param>
    public SurveyAnswerSummarizer(
        ISurveysAICompleter completer,
        ISurveyManager surveys,
        ISurveyVersionCache versions,
        ISurveyAuthorizationService authorization,
        ISurveyAnswerAggregates aggregates,
        ISurveyControlRegistry registry,
        ISiteService siteService)
    {
        _completer = completer;
        _surveys = surveys;
        _versions = versions;
        _authorization = authorization;
        _aggregates = aggregates;
        _registry = registry;
        _siteService = siteService;
    }

    /// <summary>
    /// Lists the questions of a survey's published version that hold written answers.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The questions.</returns>
    public async Task<IReadOnlyList<SurveyQuestion>> ListTextQuestionsAsync(Survey survey, CancellationToken cancellationToken)
    {
        var version = await _versions.GetPublishedAsync(survey, cancellationToken);

        return version?.Definition?.Pages?
            .SelectMany(page => page.Questions ?? [])
            .Where(question => question is not null && IsText(question.ControlType))
            .ToList() ?? [];
    }

    /// <summary>
    /// Summarizes the answers to a question.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="surveyId">The survey.</param>
    /// <param name="questionId">The question.</param>
    /// <param name="culture">The culture of the summary.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The summary or the error.</returns>
    public async Task<SurveyAnswerSummaryResult> SummarizeAsync(ClaimsPrincipal user, string surveyId, string questionId, string culture, CancellationToken cancellationToken)
    {
        var survey = string.IsNullOrEmpty(surveyId) ? null : await _surveys.FindByIdAsync(surveyId, cancellationToken);

        if (survey is null || !await _authorization.AuthorizeAsync(user, survey, SurveyOperation.ViewResponses, cancellationToken))
        {
            return new SurveyAnswerSummaryResult { Error = SurveyAnswerSummaryResult.NotFound };
        }

        var question = (await ListTextQuestionsAsync(survey, cancellationToken)).FirstOrDefault(item => string.Equals(item.Id, questionId, StringComparison.Ordinal));

        if (question is null)
        {
            return new SurveyAnswerSummaryResult { Error = SurveyAnswerSummaryResult.NotFound };
        }

        var filter = new SurveyAnswerFilter { SurveyId = survey.ItemId };
        var answers = await _aggregates.TextSamplesAsync(filter, question.Id, SurveysAIConstants.MaxSummarizedAnswers, cancellationToken);
        // The survey's minimum group size, never below the floor of the site settings.
        var settings = _siteService is null ? null : await _siteService.GetSettingsAsync<SurveySettings>();
        var minimum = Math.Max(1, CrestApps.OrchardCore.Surveys.Core.Access.SurveyAnonymity.MinimumGroupSize(survey, settings));

        if (answers.Count < minimum)
        {
            return new SurveyAnswerSummaryResult { Error = SurveyAnswerSummaryResult.TooFew, AnswerCount = answers.Count };
        }

        var input = new JsonObject
        {
            ["question"] = question.Title,
            ["answers"] = new JsonArray(answers.Select(answer => (JsonNode)answer).ToArray()),
        };

        var output = await _completer.CompleteAsync(
            SurveysAIConstants.Templates.SummarizeAnswers,
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { ["Language"] = LanguageName(culture) },
            input.ToJsonString(),
            cancellationToken);

        return output is null
            ? new SurveyAnswerSummaryResult { Error = SurveyAnswerSummaryResult.NoModel }
            : new SurveyAnswerSummaryResult { Summary = output, AnswerCount = answers.Count };
    }

    /// <summary>
    /// Gets the English name of a culture's language, for prompts.
    /// </summary>
    /// <param name="culture">The culture.</param>
    /// <returns>The name.</returns>
    public static string LanguageName(string culture)
    {
        try
        {
            return string.IsNullOrWhiteSpace(culture) ? CultureInfo.CurrentUICulture.EnglishName : CultureInfo.GetCultureInfo(culture).EnglishName;
        }
        catch (CultureNotFoundException)
        {
            return culture;
        }
    }

    // A question holds written answers when its type is in the Text category and collects an answer.
    private bool IsText(string controlType)
        => _registry.GetControl(controlType) is { } descriptor
        && descriptor.Has(SurveyControlCapabilities.Answerable)
        && string.Equals(descriptor.Category, SurveyControlCategories.Text, StringComparison.Ordinal);
}
