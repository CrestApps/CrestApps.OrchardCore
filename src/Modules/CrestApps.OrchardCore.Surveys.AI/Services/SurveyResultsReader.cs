using System.Globalization;
using System.Security.Claims;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Surveys.AI.Services;

/// <summary>
/// Reads the results of a survey for AI tools, through each question type's analyzer, for people who may read its
/// responses. Anonymous surveys with fewer responses than their minimum group size (never below the site's floor) show
/// no results. Written answers are left out unless asked for; when they are given, they are cleaned of links and
/// markdown and labeled as untrusted respondent text, because a respondent could write a prompt injection.
/// </summary>
public sealed class SurveyResultsReader
{
    private const int MaxTextSamples = 10;

    private readonly ISurveyManager _surveys;
    private readonly ISurveyVersionCache _versions;
    private readonly ISurveyAuthorizationService _authorization;
    private readonly ISurveyAnswerAggregates _aggregates;
    private readonly ISurveyControlRegistry _registry;
    private readonly ISiteService _siteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyResultsReader"/> class.
    /// </summary>
    /// <param name="surveys">The survey manager.</param>
    /// <param name="versions">The version cache.</param>
    /// <param name="authorization">The survey authorization service.</param>
    /// <param name="aggregates">The answer aggregates.</param>
    /// <param name="registry">The question type registry.</param>
    /// <param name="siteService">The site service, for the site's minimum group size floor.</param>
    public SurveyResultsReader(
        ISurveyManager surveys,
        ISurveyVersionCache versions,
        ISurveyAuthorizationService authorization,
        ISurveyAnswerAggregates aggregates,
        ISurveyControlRegistry registry,
        ISiteService siteService)
    {
        _surveys = surveys;
        _versions = versions;
        _authorization = authorization;
        _aggregates = aggregates;
        _registry = registry;
        _siteService = siteService;
    }

    /// <summary>
    /// Finds a survey by id or technical name.
    /// </summary>
    /// <param name="reference">The id or name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The survey, or <see langword="null"/>.</returns>
    public async Task<Survey> FindAsync(string reference, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return null;
        }

        return await _surveys.FindByIdAsync(reference.Trim(), cancellationToken)
            ?? await _surveys.FindByNameAsync(reference.Trim(), cancellationToken);
    }

    /// <summary>
    /// Reads the results of a survey's published version, without written answers.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="survey">The survey.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The results, or a sentence that says why there are none.</returns>
    public Task<object> ReadAsync(ClaimsPrincipal user, Survey survey, CancellationToken cancellationToken)
        => ReadAsync(user, survey, includeSamples: false, cancellationToken);

    /// <summary>
    /// Reads the results of a survey's published version.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="survey">The survey.</param>
    /// <param name="includeSamples">Whether a few written answers per question are given, as untrusted respondent text.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The results, or a sentence that says why there are none.</returns>
    public async Task<object> ReadAsync(ClaimsPrincipal user, Survey survey, bool includeSamples, CancellationToken cancellationToken)
    {
        if (survey is null || !await _authorization.AuthorizeAsync(user, survey, SurveyOperation.ViewResponses, cancellationToken))
        {
            return "The survey was not found, or the user isn't allowed to read its results.";
        }

        var version = await _versions.GetPublishedAsync(survey, cancellationToken);

        if (version is null)
        {
            return "The survey has not been published, so it has no results.";
        }

        var filter = new SurveyAnswerFilter { SurveyId = survey.ItemId };
        var responses = await _aggregates.CountResponsesAsync(filter, cancellationToken);

        // The survey's minimum group size, never below the floor of the site settings.
        var minimum = SurveyAnonymity.MinimumGroupSize(survey, await SettingsAsync());

        if (minimum > 0 && responses < minimum)
        {
            return "The survey is anonymous and has fewer responses than its minimum group size, so its results are not shown yet.";
        }

        var format = new SurveyFormatContext { Definition = version.Definition, Culture = CultureInfo.CurrentUICulture };
        var context = new SurveyAnalysisContext
        {
            Survey = survey,
            Filter = filter,
            Aggregates = _aggregates,
            Culture = CultureInfo.CurrentUICulture,
            Format = format,
            MinimumGroupSize = minimum,
        };

        var questions = new List<object>();

        foreach (var question in version.Definition.Pages?.SelectMany(page => page.Questions ?? []) ?? [])
        {
            var analyzer = question is null ? null : _registry.GetAnalyzer(question.ControlType);

            if (analyzer is null)
            {
                continue;
            }

            var summary = await analyzer.SummarizeAsync(question, context, cancellationToken);
            var choices = (question.Choices ?? []).Where(choice => choice is not null).ToDictionary(choice => choice.Id ?? string.Empty, choice => choice.Text, StringComparer.Ordinal);
            var samples = includeSamples
                ? (summary.TextSamples ?? []).Select(SurveyRespondentText.Clean).Where(text => text.Length > 0).Take(MaxTextSamples).ToList()
                : null;

            questions.Add(new
            {
                name = question.Name,
                title = question.Title,
                type = question.ControlType,
                answered = summary.Answered,
                choices = summary.ChoiceCounts.Select(pair => new { text = choices.TryGetValue(pair.Key, out var text) ? text : pair.Key, count = pair.Value }),
                numbers = summary.Numbers is null ? null : new { count = summary.Numbers.Count, average = summary.Numbers.Average, min = summary.Numbers.Min, max = summary.Numbers.Max, median = summary.Numbers.Median },
                untrustedRespondentAnswers = samples is { Count: > 0 } ? samples : null,
            });
        }

        return new
        {
            survey = survey.Title,
            name = survey.Name,
            version = version.Number,
            responses,
            notice = includeSamples ? SurveyRespondentText.Notice : null,
            questions,
        };
    }

    private async Task<SurveySettings> SettingsAsync()
        => _siteService is null ? null : await _siteService.GetSettingsAsync<SurveySettings>();
}
