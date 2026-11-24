using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Results;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Surveys.Core.Access;

/// <summary>
/// Applies the small-group rule of <see cref="SurveyGroupGate"/> to the responses a <see cref="SurveyResultsScope"/>
/// selects, counting them with one grouped query: the report, the Details and Grade pages, grading and the API decide
/// through it.
/// </summary>
public sealed class SurveyAnonymousGroups
{
    private readonly SurveyResultsQueries _queries;
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, SurveyAnonymousAttributes> _attributes = new(StringComparer.Ordinal);
    private SurveySettings _settings;
    private bool _settingsRead;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyAnonymousGroups"/> class.
    /// </summary>
    /// <param name="queries">The results queries.</param>
    /// <param name="serviceProvider">The services, for the site settings when the site service is there.</param>
    public SurveyAnonymousGroups(SurveyResultsQueries queries, IServiceProvider serviceProvider)
    {
        _queries = queries;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Gets the site settings of Surveys, or <see langword="null"/>.
    /// </summary>
    /// <returns>The settings.</returns>
    public async Task<SurveySettings> SettingsAsync()
    {
        if (!_settingsRead)
        {
            _settings = _serviceProvider.GetService<ISiteService>() is { } siteService ? await siteService.GetSettingsAsync<SurveySettings>() : null;
            _settingsRead = true;
        }

        return _settings;
    }

    /// <summary>
    /// Decides what an output of a survey may show.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="narrowing">The output's narrowing filters (versions, dates, collector, context); its status and
    /// test filters are ignored, or <see langword="null"/> for every response of the survey.</param>
    /// <param name="shown">The statuses the output lists, or empty for every status.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The decision.</returns>
    public async Task<SurveyGroupDecision> DecideAsync(Survey survey, SurveyResultsScope narrowing, IReadOnlyCollection<SurveyResponseStatus> shown, CancellationToken cancellationToken = default)
    {
        var minimum = SurveyAnonymity.MinimumGroupSize(survey, await SettingsAsync());

        if (minimum == 0)
        {
            return SurveyGroupDecision.Open;
        }

        var counts = await _queries.CountByStatusAsync(RealOf(survey, narrowing), cancellationToken);

        return SurveyGroupGate.Decide(minimum, shown ?? [], counts);
    }

    /// <summary>
    /// Gets whether one response of a survey may be opened on its own: a test response always, a real response of an
    /// anonymous survey only when its status may show among all of the survey's responses.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="response">The response.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when it may.</returns>
    public async Task<bool> ShowsAsync(Survey survey, SurveyResponse response, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (response.IsTest || !SurveyAnonymity.IsAnonymous(survey))
        {
            return true;
        }

        return (await DecideAsync(survey, null, [response.Status], cancellationToken)).Shows(response.Status, response.IsTest);
    }

    /// <summary>
    /// Gets the small-group rule of the attributes each row of a survey prints (collector, version, context kind),
    /// counted on all of the survey's completed, non-test responses: <see cref="SurveyAnonymousAttributes.Open"/> for an
    /// identified survey. Counted once per survey and scope.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The rule.</returns>
    public async Task<SurveyAnonymousAttributes> AttributesAsync(Survey survey, CancellationToken cancellationToken = default)
    {
        var minimum = SurveyAnonymity.MinimumGroupSize(survey, await SettingsAsync());

        if (minimum == 0 || string.IsNullOrEmpty(survey?.ItemId))
        {
            return SurveyAnonymousAttributes.Open;
        }

        if (!_attributes.TryGetValue(survey.ItemId, out var attributes))
        {
            _attributes[survey.ItemId] = attributes = SurveyAnonymousAttributes.Create(
                minimum,
                await _queries.CountCompletedByAsync(survey.ItemId, nameof(SurveyResponseIndex.CollectorId), cancellationToken),
                await _queries.CountCompletedByAsync(survey.ItemId, nameof(SurveyResponseIndex.VersionNumber), cancellationToken),
                await _queries.CountCompletedByAsync(survey.ItemId, nameof(SurveyResponseIndex.ContextKind), cancellationToken));
        }

        return attributes;
    }

    private static SurveyResultsScope RealOf(Survey survey, SurveyResultsScope narrowing)
        => new()
        {
            SurveyId = survey?.ItemId,
            VersionNumbers = narrowing?.VersionNumbers ?? [],
            FromUtc = narrowing?.FromUtc,
            ToUtc = narrowing?.ToUtc,
            CollectorId = narrowing?.CollectorId,
            ContextKind = narrowing?.ContextKind,
            IncludeTest = false,
            CompletedOnly = false,
            Statuses = [],
        };
}
