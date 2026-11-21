using System.Security.Claims;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Surveys.DataSources;

/// <summary>
/// Decides which surveys' rows a principal may read in the Surveys data sets, and whether it may read who responded.
/// </summary>
internal sealed class SurveyDataSetAccess
{
    private readonly SurveyStore _surveys;
    private readonly IAuthorizationService _authorizationService;
    private readonly IServiceProvider _serviceProvider;

    public SurveyDataSetAccess(
        SurveyStore surveys,
        IAuthorizationService authorizationService,
        IServiceProvider serviceProvider)
    {
        _surveys = surveys;
        _authorizationService = authorizationService;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Lists the surveys whose responses the principal of a run may read.
    /// </summary>
    /// <param name="context">The context of the run.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The surveys, by id.</returns>
    public async Task<IReadOnlyDictionary<string, Survey>> ReadableAsync(DataSourceContext context, CancellationToken cancellationToken)
    {
        var user = context?.User;
        var readable = new Dictionary<string, Survey>(StringComparer.Ordinal);

        if (user is null)
        {
            return readable;
        }

        var all = await _authorizationService.AuthorizeAsync(user, SurveysPermissions.ViewSurveyResponses);
        var authorization = _serviceProvider.GetService<ISurveyAuthorizationService>();
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var own = !all && await _authorizationService.AuthorizeAsync(user, SurveysPermissions.ViewOwnSurveyResponses);

        foreach (var survey in await _surveys.GetAllAsync(cancellationToken))
        {
            var allowed = all
                || (authorization is not null && await authorization.AuthorizeAsync(user, survey, SurveyOperation.ViewResponses, cancellationToken))
                || (own && !string.IsNullOrEmpty(userId) && string.Equals(survey.OwnerId, userId, StringComparison.Ordinal));

            if (allowed)
            {
                readable[survey.ItemId] = survey;
            }
        }

        return readable;
    }

    /// <summary>
    /// Decides whether the principal of a run may read who responded (blank for anonymous surveys whatever it holds).
    /// </summary>
    /// <param name="context">The context of the run.</param>
    /// <returns><see langword="true"/> when it may.</returns>
    public async Task<bool> CanSeeIdentityAsync(DataSourceContext context)
        => context?.User is { } user && await _authorizationService.AuthorizeAsync(user, SurveysPermissions.ExportSurveyResponses);

    /// <summary>
    /// Gets whether a survey hides who responded.
    /// </summary>
    /// <param name="survey">The survey, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when it is anonymous or unknown.</returns>
    public static bool IsAnonymous(Survey survey)
        => survey is null || survey.Access?.Anonymity == AnonymityMode.Anonymous;
}
