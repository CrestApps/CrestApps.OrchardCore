using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Learning.Core.Surveys;

/// <summary>
/// Lets the site-wide Learning roles work on the surveys course items own, and on no other survey: whoever manages every
/// course (<see cref="LearningPermissions.ManageCourses"/>) edits and publishes them, whoever views learner records sees
/// their responses, and whoever manages enrollments grants accommodations on them. The Learning roles then need no
/// site-wide Surveys permission, which would reach every survey of the site, such as HR or customer surveys.
/// </summary>
public sealed class LearningManagerSurveyAccessProvider : ISurveyAccessProvider
{
    private readonly ILearningSurveyService _learningSurveys;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningManagerSurveyAccessProvider"/> class.
    /// </summary>
    /// <param name="learningSurveys">The Learning survey service.</param>
    /// <param name="authorizationService">The authorization service.</param>
    public LearningManagerSurveyAccessProvider(
        ILearningSurveyService learningSurveys,
        IAuthorizationService authorizationService)
    {
        _learningSurveys = learningSurveys;
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Gets the Learning permission that grants an operation on every survey a course item owns.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <returns>The permission, or <see langword="null"/> when no site-wide Learning permission grants it.</returns>
    public static Permission PermissionOf(SurveyOperation operation)
        => operation switch
        {
            SurveyOperation.Manage or SurveyOperation.Publish => LearningPermissions.ManageCourses,
            SurveyOperation.ViewResponses => LearningPermissions.ViewLearnerRecords,
            SurveyOperation.ManageOverrides => LearningPermissions.ManageEnrollments,
            _ => null,
        };

    /// <inheritdoc />
    public async Task<bool> GrantsAsync(ClaimsPrincipal user, Survey survey, SurveyOperation operation, CancellationToken cancellationToken = default)
    {
        var permission = PermissionOf(operation);

        if (permission is null
            || string.IsNullOrEmpty(LearningSurveyUsers.GetUserId(user))
            || !LearningSurveyContexts.IsContentOwner(survey?.Owner))
        {
            return false;
        }

        return await _authorizationService.AuthorizeAsync(user, permission)
            && await _learningSurveys.IsOwnedByCourseItemAsync(survey);
    }
}
