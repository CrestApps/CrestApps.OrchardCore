using System.Security.Claims;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Decides operations on surveys: the global permission, then the own-survey permission for the survey's owner, then the
/// access providers of consumers.
/// </summary>
public sealed class SurveyAuthorizationService : ISurveyAuthorizationService
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IEnumerable<ISurveyAccessProvider> _accessProviders;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyAuthorizationService"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="accessProviders">The access providers.</param>
    public SurveyAuthorizationService(
        IAuthorizationService authorizationService,
        IEnumerable<ISurveyAccessProvider> accessProviders)
    {
        _authorizationService = authorizationService;
        _accessProviders = accessProviders;
    }

    /// <inheritdoc />
    public async Task<bool> AuthorizeAsync(ClaimsPrincipal user, Survey survey, SurveyOperation operation, CancellationToken cancellationToken = default)
    {
        if (user?.Identity?.IsAuthenticated != true || survey is null)
        {
            return false;
        }

        var (global, own) = PermissionsOf(operation);

        if (await _authorizationService.AuthorizeAsync(user, global))
        {
            return true;
        }

        if (own is not null
            && !string.IsNullOrEmpty(survey.OwnerId)
            && string.Equals(survey.OwnerId, user.FindFirstValue(ClaimTypes.NameIdentifier), StringComparison.Ordinal)
            && await _authorizationService.AuthorizeAsync(user, own))
        {
            return true;
        }

        foreach (var provider in _accessProviders)
        {
            if (await provider.GrantsAsync(user, survey, operation, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the global permission of an operation and the permission that grants it on the user's own surveys.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <returns>The global permission and the own permission, or <see langword="null"/> when owners get nothing more.</returns>
    public static (Permission Global, Permission Own) PermissionsOf(SurveyOperation operation)
        => operation switch
        {
            SurveyOperation.Manage => (SurveysPermissions.ManageSurveys, SurveysPermissions.ManageOwnSurveys),
            SurveyOperation.Publish => (SurveysPermissions.PublishSurveys, SurveysPermissions.ManageOwnSurveys),
            SurveyOperation.ViewResponses => (SurveysPermissions.ViewSurveyResponses, SurveysPermissions.ViewOwnSurveyResponses),
            SurveyOperation.ExportResponses => (SurveysPermissions.ExportSurveyResponses, null),
            SurveyOperation.DeleteResponses => (SurveysPermissions.DeleteSurveyResponses, SurveysPermissions.ManageOwnSurveys),
            SurveyOperation.Grade => (SurveysPermissions.GradeSurveyResponses, SurveysPermissions.ManageOwnSurveys),
            SurveyOperation.ManageOverrides => (SurveysPermissions.ManageSurveyOverrides, SurveysPermissions.ManageOwnSurveys),
            SurveyOperation.ManageInvitations => (SurveysPermissions.ManageSurveyInvitations, SurveysPermissions.ManageOwnSurveys),
            _ => (SurveysPermissions.ManageSurveys, null),
        };
}
