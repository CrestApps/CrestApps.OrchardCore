using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DataSources;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Surveys.DataSources;

/// <summary>
/// Decides which surveys' rows a principal may read in the Surveys data sets (by <see cref="SurveyAccessScopes"/>, from
/// the survey index), and reads what anonymous surveys need: the site's minimum group size settings and the time zone
/// their days are read in.
/// </summary>
internal sealed class SurveyDataSetAccess
{
    private readonly SurveyAccessScopes _scopes;
    private readonly SurveyIndexReader _reader;
    private readonly IServiceProvider _serviceProvider;

    public SurveyDataSetAccess(
        SurveyAccessScopes scopes,
        SurveyIndexReader reader,
        IServiceProvider serviceProvider)
    {
        _scopes = scopes;
        _reader = reader;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Lists the surveys whose responses the principal of a run may read, as summaries read from the survey index (no
    /// survey document is loaded), and whether that is every survey, so the read needs no survey filter.
    /// </summary>
    /// <param name="context">The context of the run.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Whether every survey is readable, and the readable surveys, by id.</returns>
    public async Task<(bool All, IReadOnlyDictionary<string, Survey> Surveys)> ReadableAsync(DataSourceContext context, CancellationToken cancellationToken)
    {
        var readable = new Dictionary<string, Survey>(StringComparer.Ordinal);
        var scope = await ScopeAsync(context, cancellationToken);

        if (scope.IsEmpty)
        {
            return (false, readable);
        }

        foreach (var survey in await _reader.ListAsync(scope.All ? null : scope.SurveyIds, publishedOnly: false, cancellationToken))
        {
            readable[survey.ItemId] = survey;
        }

        return (scope.All, readable);
    }

    /// <summary>
    /// Lists the published surveys whose responses the principal of a run may read, as summaries read from the survey
    /// index.
    /// </summary>
    /// <param name="context">The context of the run.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The surveys.</returns>
    public async Task<IReadOnlyList<Survey>> ReadablePublishedAsync(DataSourceContext context, CancellationToken cancellationToken)
    {
        var scope = await ScopeAsync(context, cancellationToken);

        return scope.IsEmpty ? [] : await _reader.ListAsync(scope.All ? null : scope.SurveyIds, publishedOnly: true, cancellationToken);
    }

    /// <summary>
    /// Decides whether the principal of a run may read the responses of one survey.
    /// </summary>
    /// <param name="context">The context of the run.</param>
    /// <param name="surveyId">The survey id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when it may.</returns>
    public async Task<bool> CanReadAsync(DataSourceContext context, string surveyId, CancellationToken cancellationToken)
        => (await ScopeAsync(context, cancellationToken)).Contains(surveyId);

    private Task<SurveyAccessScope> ScopeAsync(DataSourceContext context, CancellationToken cancellationToken)
        => context?.User is { } user
            ? _scopes.ResolveAsync(user, SurveyOperation.ViewResponses, cancellationToken)
            : Task.FromResult(SurveyAccessScope.None);

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
