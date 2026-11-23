using System.Security.Claims;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.AspNetCore.Authorization;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Lets a user with Grade own survey responses grade the surveys they own from the whole grading queue, not only from
/// each survey's own link. A user with the global Grade permission sees every survey anyway and is left out.
/// </summary>
public sealed class OwnSurveysGradingScope : ISurveyGradingScope
{
    private readonly IAuthorizationService _authorizationService;
    private readonly SurveyStore _surveyStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="OwnSurveysGradingScope"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="surveyStore">The survey store.</param>
    public OwnSurveysGradingScope(IAuthorizationService authorizationService, SurveyStore surveyStore)
    {
        _authorizationService = authorizationService;
        _surveyStore = surveyStore;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<string>> GetGradableSurveyIdsAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        var userId = user?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId)
            || await _authorizationService.AuthorizeAsync(user, SurveysPermissions.GradeSurveyResponses)
            || !await _authorizationService.AuthorizeAsync(user, SurveysPermissions.GradeOwnSurveyResponses))
        {
            return [];
        }

        // The survey index has no owner column yet, so this reads the surveys; switch to an indexed owner query once one
        // exists.
        return (await _surveyStore.GetAllAsync(cancellationToken))
            .Where(survey => string.Equals(survey.OwnerId, userId, StringComparison.Ordinal))
            .Select(survey => survey.ItemId)
            .ToList();
    }
}
