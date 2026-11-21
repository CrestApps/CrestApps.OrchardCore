using System.Globalization;
using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Surveys.AI.Services;

/// <summary>
/// Suggests a score and a comment to a grader for an answer that needs a grader, such as an essay, and records that the
/// grader was given AI help so the grade they save is marked AI-assisted.
/// </summary>
public sealed class SurveyScoringAssistant
{
    private readonly ISurveysAICompleter _completer;
    private readonly ISurveyResponseQueries _responses;
    private readonly ISurveyVersionCache _versions;
    private readonly ISurveyManager _surveys;
    private readonly ISurveyAuthorizationService _authorization;
    private readonly ISurveyControlRegistry _registry;
    private readonly ISurveyGradingService _grading;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyScoringAssistant"/> class.
    /// </summary>
    /// <param name="completer">The AI completer.</param>
    /// <param name="responses">The response queries.</param>
    /// <param name="versions">The version cache.</param>
    /// <param name="surveys">The survey manager.</param>
    /// <param name="authorization">The survey authorization service.</param>
    /// <param name="registry">The question type registry.</param>
    /// <param name="grading">The grading service.</param>
    public SurveyScoringAssistant(
        ISurveysAICompleter completer,
        ISurveyResponseQueries responses,
        ISurveyVersionCache versions,
        ISurveyManager surveys,
        ISurveyAuthorizationService authorization,
        ISurveyControlRegistry registry,
        ISurveyGradingService grading)
    {
        _completer = completer;
        _responses = responses;
        _versions = versions;
        _surveys = surveys;
        _authorization = authorization;
        _registry = registry;
        _grading = grading;
    }

    /// <summary>
    /// Suggests a score and a comment for one answer of a response.
    /// </summary>
    /// <param name="user">The grader.</param>
    /// <param name="responseId">The response.</param>
    /// <param name="questionId">The question.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The suggestion or the error.</returns>
    public async Task<SurveyScoringHelpResult> SuggestAsync(ClaimsPrincipal user, string responseId, string questionId, CancellationToken cancellationToken)
    {
        var response = string.IsNullOrEmpty(responseId) ? null : await _responses.FindByIdAsync(responseId, cancellationToken);
        var survey = response is null ? null : await _surveys.FindByIdAsync(response.SurveyId, cancellationToken);

        if (survey is null ||
            response.Status != SurveyResponseStatus.Completed ||
            user?.Identity?.IsAuthenticated != true ||
            !await _authorization.AuthorizeAsync(user, survey, SurveyOperation.Grade, cancellationToken))
        {
            return new SurveyScoringHelpResult { Error = SurveyScoringHelpResult.NotFound };
        }

        var version = await _versions.GetAsync(response.SurveyVersionId, cancellationToken);
        var question = version?.Definition?.Pages?
            .SelectMany(page => page.Questions ?? [])
            .FirstOrDefault(item => string.Equals(item?.Id, questionId, StringComparison.Ordinal));
        var descriptor = question is null ? null : _registry.GetControl(question.ControlType);

        if (descriptor is null || !descriptor.Has(SurveyControlCapabilities.ManuallyGraded))
        {
            return new SurveyScoringHelpResult { Error = SurveyScoringHelpResult.NotFound };
        }

        response.Answers.TryGetValue(question.Id, out var answer);

        var text = Text(question, answer, version.Definition);

        if (string.IsNullOrWhiteSpace(text))
        {
            return new SurveyScoringHelpResult { Error = SurveyScoringHelpResult.NoText };
        }

        var maxPoints = answer?.MaxPoints ?? question.Scoring?.Points ?? version.Definition.Scoring?.DefaultPoints ?? 1;
        var input = new JsonObject
        {
            ["question"] = question.Title,
            ["guidance"] = question.Scoring?.GraderGuidance ?? string.Empty,
            ["maxPoints"] = maxPoints,
            ["answer"] = text,
        };

        var output = await _completer.CompleteAsync(
            SurveysAIConstants.Templates.ScoreAnswer,
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["MaxPoints"] = maxPoints.ToString("0.##", CultureInfo.InvariantCulture),
            },
            input.ToJsonString(),
            cancellationToken);

        if (output is null)
        {
            return new SurveyScoringHelpResult { Error = SurveyScoringHelpResult.NoModel };
        }

        var suggestion = SurveyScoringSuggestionParser.Parse(output, maxPoints);

        if (suggestion is null)
        {
            return new SurveyScoringHelpResult { Error = SurveyScoringHelpResult.NoSuggestion };
        }

        // The server records that the grader was given a suggestion: the grade they save next is AI-assisted.
        await _grading.RecordScoringHelpAsync(response.ItemId, question.Id, cancellationToken);

        return new SurveyScoringHelpResult { Suggestion = suggestion };
    }

    private string Text(SurveyQuestion question, SurveyAnswer answer, SurveyDefinition definition)
    {
        if (answer is null)
        {
            return null;
        }

        var handler = _registry.GetHandler(question.ControlType);

        return handler?.FormatAnswer(question, answer, new SurveyFormatContext { Definition = definition, Culture = CultureInfo.InvariantCulture })
            ?? string.Join(Environment.NewLine, answer.Values ?? []);
    }
}
