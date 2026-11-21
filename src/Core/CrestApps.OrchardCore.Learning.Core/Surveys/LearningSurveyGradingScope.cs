using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.AspNetCore.Authorization;

namespace CrestApps.OrchardCore.Learning.Core.Surveys;

/// <summary>
/// Lets course staff (graders, instructors and assistants) grade the surveys and quizzes their course items own.
/// </summary>
public sealed class LearningSurveyGradingScope : ISurveyGradingScope
{
    private static readonly string[] _gradingRoles =
    [
        LearningConstants.CourseStaffRoles.Grader,
        LearningConstants.CourseStaffRoles.Instructor,
        LearningConstants.CourseStaffRoles.Assistant,
    ];

    private readonly ICourseStructureService _courseStructure;
    private readonly ISurveyManager _surveyManager;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningSurveyGradingScope"/> class.
    /// </summary>
    /// <param name="courseStructure">The course structure service.</param>
    /// <param name="surveyManager">The survey manager.</param>
    /// <param name="authorizationService">The authorization service.</param>
    public LearningSurveyGradingScope(
        ICourseStructureService courseStructure,
        ISurveyManager surveyManager,
        IAuthorizationService authorizationService)
    {
        _courseStructure = courseStructure;
        _surveyManager = surveyManager;
        _authorizationService = authorizationService;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<string>> GetGradableSurveyIdsAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        var userId = user?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId) || !await _authorizationService.AuthorizeAsync(user, LearningPermissions.ManageOwnCourses))
        {
            return [];
        }

        var surveyIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var courseId in await _courseStructure.GetStaffCourseIdsAsync(userId, _gradingRoles))
        {
            var course = await _courseStructure.GetCourseAsync(courseId);

            if (course is null)
            {
                continue;
            }

            var outline = await _courseStructure.GetOutlineAsync(course);

            foreach (var entry in outline.Entries)
            {
                if (entry.Item is null)
                {
                    continue;
                }

                foreach (var survey in await _surveyManager.ListByOwnerAsync(LearningSurveyContexts.Owner(entry.Item.ContentItemId), cancellationToken))
                {
                    surveyIds.Add(survey.ItemId);
                }
            }
        }

        return surveyIds;
    }
}
