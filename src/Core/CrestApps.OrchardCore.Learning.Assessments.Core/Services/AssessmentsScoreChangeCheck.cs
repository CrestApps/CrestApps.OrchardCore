using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.AspNetCore.Authorization;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Services;

/// <summary>
/// Requires Override grades to change a score that is given on a survey a course item owns (a quiz attempt or a course
/// survey): a change of the total score, a regrade, grading a graded response again or scoring older versions with
/// another key, as for graded coursework. Surveys no course item owns are left to Surveys.
/// </summary>
public sealed class AssessmentsScoreChangeCheck : ISurveyScoreChangeCheck
{
    private readonly ILearningSurveyService _learningSurveys;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AssessmentsScoreChangeCheck"/> class.
    /// </summary>
    /// <param name="learningSurveys">The Learning survey service.</param>
    /// <param name="authorizationService">The authorization service.</param>
    public AssessmentsScoreChangeCheck(
        ILearningSurveyService learningSurveys,
        IAuthorizationService authorizationService)
    {
        _learningSurveys = learningSurveys;
        _authorizationService = authorizationService;
    }

    /// <inheritdoc />
    public async Task<bool> AllowsAsync(ClaimsPrincipal user, Survey survey, SurveyResponse response, CancellationToken cancellationToken = default)
    {
        if (!LearningSurveyContexts.IsContentOwner(survey?.Owner) || !await _learningSurveys.IsOwnedByCourseItemAsync(survey))
        {
            return true;
        }

        return user is not null && await _authorizationService.AuthorizeAsync(user, AssessmentPermissions.OverrideGrades);
    }
}
