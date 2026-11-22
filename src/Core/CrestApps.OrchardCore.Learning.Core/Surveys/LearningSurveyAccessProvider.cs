using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.AspNetCore.Authorization;

namespace CrestApps.OrchardCore.Learning.Core.Surveys;

/// <summary>
/// Lets course staff work on the surveys their course items own without the global Surveys permissions: instructors and
/// assistants manage and publish them and see their responses, and graders grade them.
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

        var item = await _learningSurveys.FindOwnerItemAsync(survey);
        var courseId = item is null ? null : _courseStructure.GetCourseId(item);

        if (string.IsNullOrEmpty(courseId))
        {
            return false;
        }

        var roles = operation switch
        {
            SurveyOperation.Grade or SurveyOperation.ViewResponses => _gradingRoles,
            SurveyOperation.ExportResponses or SurveyOperation.ManageInvitations => [],
            _ => _editingRoles,
        };

        return roles.Length > 0 && await _courseStructure.IsCourseStaffAsync(userId, courseId, roles);
    }
}
