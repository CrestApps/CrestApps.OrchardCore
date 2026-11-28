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

        // The courses the user grades decide the scope. Their items are the latest version of every item they contain,
        // published or not (a draft quiz or a quiz of a course that was unpublished keeps its attempts gradable), and the
        // surveys those items own are looked up at once.
        var courseIds = await _courseStructure.GetStaffCourseIdsAsync(userId, _gradingRoles);

        if (courseIds.Count == 0)
        {
            return [];
        }

        var owners = (await _courseStructure.GetContainedItemsAsync(courseIds) ?? [])
            .Where(item => item is not null)
            .Select(item => item.ContentItemId)
            .Distinct(StringComparer.Ordinal)
            .Select(LearningSurveyContexts.Owner)
            .ToArray();

        if (owners.Length == 0)
        {
            return [];
        }

        return (await _surveyManager.ListByOwnersAsync(owners, cancellationToken) ?? [])
            .Select(survey => survey.ItemId)
            .ToHashSet(StringComparer.Ordinal);
    }
}
