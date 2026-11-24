using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Services;

/// <summary>
/// Lets whoever grades the work of every course grade and read the responses of the surveys course items own (quiz
/// attempts and course surveys), and whoever may override grades void and delete them, without a site-wide Surveys
/// permission that would reach every survey of the site.
/// </summary>
public sealed class AssessmentsSurveyAccessProvider : ISurveyAccessProvider
{
    private readonly ILearningSurveyService _learningSurveys;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AssessmentsSurveyAccessProvider"/> class.
    /// </summary>
    /// <param name="learningSurveys">The Learning survey service.</param>
    /// <param name="authorizationService">The authorization service.</param>
    public AssessmentsSurveyAccessProvider(
        ILearningSurveyService learningSurveys,
        IAuthorizationService authorizationService)
    {
        _learningSurveys = learningSurveys;
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Gets the Assessments permission that grants an operation on every survey a course item owns.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <returns>The permission, or <see langword="null"/> when no Assessments permission grants it.</returns>
    public static Permission PermissionOf(SurveyOperation operation)
        => operation switch
        {
            SurveyOperation.Grade or SurveyOperation.ViewResponses => AssessmentPermissions.GradeAssessments,
            SurveyOperation.DeleteResponses or SurveyOperation.RetryUpkeep => AssessmentPermissions.OverrideGrades,
            _ => null,
        };

    /// <inheritdoc />
    public async Task<bool> GrantsAsync(ClaimsPrincipal user, Survey survey, SurveyOperation operation, CancellationToken cancellationToken = default)
    {
        var permission = PermissionOf(operation);

        if (permission is null
            || user?.Identity?.IsAuthenticated != true
            || !LearningSurveyContexts.IsContentOwner(survey?.Owner))
        {
            return false;
        }

        return await _authorizationService.AuthorizeAsync(user, permission)
            && await _learningSurveys.IsOwnedByCourseItemAsync(survey);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Lists every course item as an owner, from the content index, so a scope check never asks about each survey.
    /// </remarks>
    public async Task<SurveyAccessGrants> ListGrantsAsync(ClaimsPrincipal user, SurveyOperation operation, CancellationToken cancellationToken = default)
    {
        var permission = PermissionOf(operation);

        if (permission is null
            || user?.Identity?.IsAuthenticated != true
            || !await _authorizationService.AuthorizeAsync(user, permission))
        {
            return SurveyAccessGrants.None;
        }

        return new SurveyAccessGrants
        {
            Owners = await _learningSurveys.ListCourseItemOwnersAsync(cancellationToken) ?? [],
        };
    }
}
