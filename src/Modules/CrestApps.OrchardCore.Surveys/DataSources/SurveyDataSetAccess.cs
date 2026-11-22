using System.Security.Claims;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DataSources;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Surveys.DataSources;

/// <summary>
/// Decides which surveys' rows a principal may read in the Surveys data sets, and reads what anonymous surveys need:
/// the site's minimum group size settings and the time zone their days are read in.
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
    /// Reads the site settings of Surveys and the site's time zone, each optional.
    /// </summary>
    /// <returns>The settings, or <see langword="null"/>, and the time zone (UTC when unknown).</returns>
    public async Task<(SurveySettings Settings, TimeZoneInfo TimeZone)> AnonymityAsync()
    {
        var settings = _serviceProvider.GetService<ISiteService>() is { } siteService ? await siteService.GetSettingsAsync<SurveySettings>() : null;
        var timeZone = TimeZoneInfo.Utc;

        if (_serviceProvider.GetService<ILocalClock>() is { } localClock
            && (await localClock.GetLocalTimeZoneAsync())?.TimeZoneId is { Length: > 0 } timeZoneId
            && TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var found))
        {
            timeZone = found;
        }

        return (settings, timeZone);
    }

    /// <summary>
    /// Gets whether a survey hides who responded.
    /// </summary>
    /// <param name="survey">The survey, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when it is anonymous or unknown.</returns>
    public static bool IsAnonymous(Survey survey)
        => survey is null || survey.Access?.Anonymity == AnonymityMode.Anonymous;
}
