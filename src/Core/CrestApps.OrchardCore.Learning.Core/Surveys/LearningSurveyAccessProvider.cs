using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Learning.Core.Surveys;

/// <summary>
/// Lets course staff work on the surveys their course items own without the global Surveys permissions: instructors and
/// assistants manage and publish them and see their responses, and graders grade them. Voiding or deleting a response of
/// a graded survey (a quiz, or a scored course survey) changes a given grade, so it also needs every score change check
/// to allow it (with Assessments, Override grades); the responses of a course survey that isn't graded stay with the
/// instructors and assistants.
/// </summary>
public sealed class LearningSurveyAccessProvider : ISurveyAccessProvider
{
    private static readonly string[] _editingRoles =
    [
        LearningConstants.CourseStaffRoles.Instructor,
        LearningConstants.CourseStaffRoles.Assistant,
    ];

    private static readonly string[] _gradingRoles =
    [
        LearningConstants.CourseStaffRoles.Instructor,
        LearningConstants.CourseStaffRoles.Assistant,
        LearningConstants.CourseStaffRoles.Grader,
    ];

    private readonly ILearningSurveyService _learningSurveys;
    private readonly ICourseStructureService _courseStructure;
    private readonly IAuthorizationService _authorizationService;
    private readonly IEnumerable<ISurveyScoreChangeCheck> _scoreChangeChecks;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningSurveyAccessProvider"/> class.
    /// </summary>
    /// <param name="learningSurveys">The Learning survey service.</param>
    /// <param name="courseStructure">The course structure service.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="scoreChangeChecks">The checks a change of a given score must pass.</param>
    /// <param name="serviceProvider">The services, for the published version of a survey.</param>
    public LearningSurveyAccessProvider(
        ILearningSurveyService learningSurveys,
        ICourseStructureService courseStructure,
        IAuthorizationService authorizationService,
        IEnumerable<ISurveyScoreChangeCheck> scoreChangeChecks,
        IServiceProvider serviceProvider)
    {
        _learningSurveys = learningSurveys;
        _courseStructure = courseStructure;
        _authorizationService = authorizationService;
        _scoreChangeChecks = scoreChangeChecks;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public async Task<bool> GrantsAsync(ClaimsPrincipal user, Survey survey, SurveyOperation operation, CancellationToken cancellationToken = default)
    {
        var userId = LearningSurveyUsers.GetUserId(user);

        if (string.IsNullOrEmpty(userId) || !LearningSurveyContexts.IsContentOwner(survey?.Owner))
        {
            return false;
        }

        // Being named on a course is not enough: course staff work on it only with a Learning permission too.
        if (!await _authorizationService.AuthorizeAsync(user, LearningPermissions.ManageOwnCourses))
        {
            return false;
        }

        // Grading and reading responses follow the grading scope, which reads the latest items, published or not: a grader
        // of an unpublished quiz gets AI help, file answers and names along with the grading itself. Changing the survey
        // stays tied to the published item.
        var grading = operation is SurveyOperation.Grade or SurveyOperation.ViewResponses;
        var item = grading
            ? await _learningSurveys.FindLatestOwnerItemAsync(survey)
            : await _learningSurveys.FindOwnerItemAsync(survey);
        var courseId = item is null ? null : _courseStructure.GetCourseId(item);

        if (string.IsNullOrEmpty(courseId))
        {
            return false;
        }

        var roles = RolesFor(operation);

        if (roles.Length == 0 || !await _courseStructure.IsCourseStaffAsync(userId, courseId, roles))
        {
            return false;
        }

        // Voiding or deleting an attempt changes a given grade: the earlier attempt becomes the grade, or the attempt is
        // given back. On a graded survey that needs what changing a score needs.
        return operation != SurveyOperation.DeleteResponses
            || !await IsGradedAsync(survey, cancellationToken)
            || await ScoreChangesAllowedAsync(user, survey, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SurveyAccessGrants> ListGrantsAsync(ClaimsPrincipal user, SurveyOperation operation, CancellationToken cancellationToken = default)
    {
        var userId = LearningSurveyUsers.GetUserId(user);
        var roles = RolesFor(operation);

        if (string.IsNullOrEmpty(userId) || roles.Length == 0 || !await _authorizationService.AuthorizeAsync(user, LearningPermissions.ManageOwnCourses))
        {
            return SurveyAccessGrants.None;
        }

        // Deleting responses depends on each survey (graded or not) and the score change checks it must pass: each survey
        // is asked.
        if (operation == SurveyOperation.DeleteResponses && _scoreChangeChecks.Any())
        {
            return null;
        }

        // The surveys of the items of the courses the user is staff on, by their owners: no survey is loaded.
        var courseIds = await _courseStructure.GetStaffCourseIdsAsync(userId, roles);

        if (courseIds.Count == 0)
        {
            return SurveyAccessGrants.None;
        }

        return new SurveyAccessGrants
        {
            Owners = (await _courseStructure.GetContainedItemsAsync(courseIds) ?? [])
                .Where(item => item is not null && !string.IsNullOrEmpty(item.ContentItemId))
                .Select(item => item.ContentItemId)
                .Distinct(StringComparer.Ordinal)
                .Select(LearningSurveyContexts.Owner)
                .ToList(),
        };
    }

    /// <summary>
    /// Gets whether a survey gives grades: a quiz, or a survey whose draft or published version is scored.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the survey is graded.</returns>
    private async Task<bool> IsGradedAsync(Survey survey, CancellationToken cancellationToken)
    {
        if (string.Equals(survey.Kind, SurveyKinds.Quiz, StringComparison.Ordinal) || survey.Draft?.Scoring?.Enabled == true)
        {
            return true;
        }

        if (string.IsNullOrEmpty(survey.PublishedVersionId) || _serviceProvider.GetService<ISurveyVersionCache>() is not { } versions)
        {
            return false;
        }

        return (await versions.GetAsync(survey.PublishedVersionId, cancellationToken))?.Definition?.Scoring?.Enabled == true;
    }

    private async Task<bool> ScoreChangesAllowedAsync(ClaimsPrincipal user, Survey survey, CancellationToken cancellationToken)
    {
        foreach (var check in _scoreChangeChecks)
        {
            if (!await check.AllowsAsync(user, survey, null, cancellationToken))
            {
                return false;
            }
        }

        return true;
    }

    private static string[] RolesFor(SurveyOperation operation)
        => operation switch
        {
            SurveyOperation.Grade or SurveyOperation.ViewResponses => _gradingRoles,
            SurveyOperation.ExportResponses or SurveyOperation.ManageInvitations => [],
            _ => _editingRoles,
        };
}
