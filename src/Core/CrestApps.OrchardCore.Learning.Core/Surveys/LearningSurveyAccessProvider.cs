using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.AspNetCore.Authorization;

namespace CrestApps.OrchardCore.Learning.Core.Surveys;

/// <summary>
/// Lets course staff work on the surveys their course items own without the global Surveys permissions: instructors and
/// assistants manage and publish them and see their responses, void and delete them and have upkeep retry them, and
/// graders grade them. Voiding or deleting a completed, scored response changes a given grade, so Surveys also asks every
/// score change check about that response (<c>SurveyScoreChangeChecks.RemovingChangesGivenScore</c>; with Assessments,
/// Override grades); an attempt in progress, abandoned or a test, and the responses of a survey that isn't scored, stay
/// with the instructors and assistants.
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

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningSurveyAccessProvider"/> class.
    /// </summary>
    /// <param name="learningSurveys">The Learning survey service.</param>
    /// <param name="courseStructure">The course structure service.</param>
    /// <param name="authorizationService">The authorization service.</param>
    public LearningSurveyAccessProvider(
        ILearningSurveyService learningSurveys,
        ICourseStructureService courseStructure,
        IAuthorizationService authorizationService)
    {
        _learningSurveys = learningSurveys;
        _courseStructure = courseStructure;
        _authorizationService = authorizationService;
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

        // Voiding or deleting a completed, scored attempt changes a given grade: Surveys asks the score change checks
        // about that response, so the survey-level grant here never refuses the attempts that carry no grade.
        return roles.Length > 0 && await _courseStructure.IsCourseStaffAsync(userId, courseId, roles);
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

    private static string[] RolesFor(SurveyOperation operation)
        => operation switch
        {
            SurveyOperation.Grade or SurveyOperation.ViewResponses => _gradingRoles,
            SurveyOperation.ExportResponses or SurveyOperation.ManageInvitations => [],
            _ => _editingRoles,
        };
}
