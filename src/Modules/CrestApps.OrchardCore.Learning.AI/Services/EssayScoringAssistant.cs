using System.Globalization;
using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Coursework;
using CrestApps.OrchardCore.Learning.Coursework.Core.Services;
using CrestApps.OrchardCore.Learning.Coursework.Models;
using CrestApps.OrchardCore.Learning.Coursework.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Html.Models;

namespace CrestApps.OrchardCore.Learning.AI.Services;

/// <summary>
/// The outcome of a scoring help request.
/// </summary>
public sealed class EssayScoringResult
{
    /// <summary>
    /// The work was not found, or the person may not grade it.
    /// </summary>
    public const string NotFound = "NotFound";

    /// <summary>
    /// No AI deployment is set up.
    /// </summary>
    public const string NoModel = "NoModel";

    /// <summary>
    /// The work has no text to read (only files, or nothing).
    /// </summary>
    public const string NoText = "NoText";

    /// <summary>
    /// The model's answer held nothing usable.
    /// </summary>
    public const string NoSuggestion = "NoSuggestion";

    /// <summary>
    /// Gets or sets the error, or <see langword="null"/>.
    /// </summary>
    public string Error { get; set; }

    /// <summary>
    /// Gets or sets the suggestion.
    /// </summary>
    public EssayScoringSuggestion Suggestion { get; set; }
}

/// <summary>
/// Scoring help for coursework graders: asks the model for a suggested score and feedback for a coursework text
/// submission (against its rubric or out of its points). Quiz essay answers get scoring help on the Surveys grading
/// page. It reads the
/// work and checks that the person may grade it, and it never writes a grade: the grader confirms or changes the
/// suggestion and saves it. The learner's name is not sent to the model.
/// </summary>
public sealed class EssayScoringAssistant
{
    private readonly ILearningAICompleter _completer;
    private readonly IServiceProvider _services;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="EssayScoringAssistant"/> class.
    /// </summary>
    public EssayScoringAssistant(
        ILearningAICompleter completer,
        IServiceProvider services,
        IAuthorizationService authorizationService)
    {
        _completer = completer;
        _services = services;
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Suggests a score for a coursework submission's text.
    /// </summary>
    public async Task<EssayScoringResult> SuggestForSubmissionAsync(ClaimsPrincipal user, string submissionId, CancellationToken cancellationToken)
    {
        var submissions = _services.GetService<ISubmissionService>();
        var submission = submissions is null || string.IsNullOrEmpty(submissionId) ? null : await submissions.FindByIdAsync(submissionId, cancellationToken);

        if (submission is null || !await CanUseAsync(user, submission.UserId, submission.CourseId))
        {
            return new EssayScoringResult { Error = EssayScoringResult.NotFound };
        }

        if (string.IsNullOrWhiteSpace(submission.Text))
        {
            return new EssayScoringResult { Error = EssayScoringResult.NoText };
        }

        var contentSource = _services.GetRequiredService<ICourseworkContentSource>();
        var coursework = await contentSource.GetPublishedAsync(submission.CourseworkId);
        var part = coursework?.Get<CourseworkPart>(nameof(CourseworkPart)) ?? new CourseworkPart();
        var rubricId = submission.Status == SubmissionStatus.Graded && !string.IsNullOrEmpty(submission.RubricId) ? submission.RubricId : part.RubricId;
        var rubric = string.IsNullOrEmpty(rubricId) ? null : (await contentSource.GetPublishedAsync(rubricId))?.Get<RubricPart>(nameof(RubricPart));
        var criteria = rubric?.Criteria is { Count: > 0 } list ? list : null;

        var input = new JsonObject
        {
            ["task"] = (coursework?.DisplayText ?? submission.CourseworkTitle) + "\n" + (QuizResultExplainer.Text(coursework?.Get<HtmlBodyPart>(nameof(HtmlBodyPart))?.Html) ?? string.Empty),
            ["maxPoints"] = part.MaxPoints,
            ["answer"] = submission.Text,
        };

        if (criteria is not null)
        {
            input["rubric"] = new JsonArray(criteria.Select(criterion => (JsonNode)new JsonObject
            {
                ["id"] = criterion.Id,
                ["title"] = criterion.Title,
                ["description"] = criterion.Description,
                ["levels"] = new JsonArray(criterion.Levels.Select(level => (JsonNode)new JsonObject
                {
                    ["id"] = level.Id,
                    ["title"] = level.Title,
                    ["points"] = level.Points,
                }).ToArray()),
            }).ToArray());
        }

        var result = await SuggestAsync(input, part.MaxPoints, criteria, CourseworkConstants.MaxFeedbackLength, cancellationToken);

        // The server records that this grader was given a suggestion: the grade they save next is AI-assisted (review
        // round 10, item 1).
        if (result.Suggestion is not null && user?.FindFirstValue(ClaimTypes.NameIdentifier) is { Length: > 0 } grader)
        {
            await submissions.RecordScoringHelpAsync(submission.ItemId, grader, cancellationToken);
        }

        return result;
    }

    private async Task<EssayScoringResult> SuggestAsync(JsonObject input, decimal maxPoints, IReadOnlyList<RubricCriterion> criteria, int maxFeedbackLength, CancellationToken cancellationToken)
    {
        var answer = await _completer.CompleteAsync(
            LearningAIConstants.Templates.ScoreEssay,
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["MaxPoints"] = maxPoints.ToString("0.##", CultureInfo.InvariantCulture),
                ["HasRubric"] = criteria is not null,
            },
            input.ToJsonString(),
            cancellationToken);

        if (answer is null)
        {
            return new EssayScoringResult { Error = EssayScoringResult.NoModel };
        }

        var suggestion = EssayScoringParser.Parse(answer, maxPoints, criteria, maxFeedbackLength);

        return suggestion is null
            ? new EssayScoringResult { Error = EssayScoringResult.NoSuggestion }
            : new EssayScoringResult { Suggestion = suggestion };
    }

    private async Task<bool> CanUseAsync(ClaimsPrincipal user, string learnerId, string courseId)
    {
        if (user?.Identity?.IsAuthenticated != true || !await _authorizationService.AuthorizeAsync(user, LearningAIPermissions.UseScoringHelp))
        {
            return false;
        }

        var scope = _services.GetService<GradingScope>();

        return scope is not null && await scope.CanGradeAsync(user, learnerId, courseId);
    }
}
