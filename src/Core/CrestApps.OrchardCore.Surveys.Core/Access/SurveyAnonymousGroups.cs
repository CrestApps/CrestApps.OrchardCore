using CrestApps.OrchardCore.Surveys.Core.Results;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
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
    private readonly Dictionary<string, IReadOnlyList<SurveyResponseGroup>> _hiddenGroups = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyCollection<SurveyResponseGroup>> _allGroups = new(StringComparer.Ordinal);
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
    /// <returns>The decision. The status counts leave out the groups the output leaves out
    /// (<see cref="ConcealedGroupsAsync"/>), so a status shows only when enough of the responses the output can list share it.</returns>
    public async Task<SurveyGroupDecision> DecideAsync(Survey survey, SurveyResultsScope narrowing, IReadOnlyCollection<SurveyResponseStatus> shown, CancellationToken cancellationToken = default)
    {
        var minimum = SurveyAnonymity.MinimumGroupSize(survey, await SettingsAsync());

        if (minimum == 0)
        {
            return SurveyGroupDecision.Open;
        }

        if (await ConcealedGroupsAsync(survey, narrowing, cancellationToken) is not { } concealed)
        {
            return new SurveyGroupDecision { MinimumGroupSize = minimum, HidesAll = true };
        }

        var counts = await _queries.CountByStatusAsync(RealOf(survey, narrowing, concealed), cancellationToken);

        return SurveyGroupGate.Decide(minimum, shown ?? [], counts);
    }

    /// <summary>
    /// Counts the real (non-test) responses of a survey that the responses list shows with no filter: under the
    /// small-group rule, none until the completed responses reach the minimum group size, then every status but those too
    /// few responses share, leaving out the groups the list leaves out (<see cref="HiddenGroupsAsync"/>). An identified
    /// survey counts all of its real responses.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The count, or <see langword="null"/> when the list shows none of the survey's responses yet.</returns>
    public async Task<long?> CountShownAsync(Survey survey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(survey);

        var decision = await DecideAsync(survey, narrowing: null, shown: [], cancellationToken);

        if (decision.HidesAll || await ConcealedGroupsAsync(survey, narrowing: null, cancellationToken) is not { } concealed)
        {
            return null;
        }

        var counts = await _queries.CountByStatusAsync(RealOf(survey, narrowing: null, concealed), cancellationToken);

        return counts.Where(entry => !decision.HiddenStatuses.Contains(entry.Key)).Sum(entry => entry.Value);
    }

    /// <summary>
    /// Gets whether one response of a survey may be opened on its own (Details, Grade, the scoring assistant): a test
    /// response always; a real response of an anonymous survey only when its status may show among all of the survey's
    /// responses and its joint key (collector, version, context kind) isn't hidden (<see cref="SurveyAnonymousAttributes"/>).
    /// Both refusals are one answer, so a refusal doesn't tell which of a response's groups is small.
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

        return (await DecideAsync(survey, null, [response.Status], cancellationToken)).Shows(response.Status, response.IsTest)
            && !(await AttributesAsync(survey, cancellationToken)).Of(response).Hidden;
    }

    /// <summary>
    /// Gets the minimum group size of a survey, or 0 for an identified survey.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <returns>The minimum.</returns>
    public async Task<int> MinimumGroupSizeAsync(Survey survey)
        => SurveyAnonymity.MinimumGroupSize(survey, await SettingsAsync());

    /// <summary>
    /// Gets the small-group rule of the attributes each row of a survey prints (collector, version, context kind),
    /// counted by their joint key on all of the survey's completed, non-test responses:
    /// <see cref="SurveyAnonymousAttributes.Open"/> for an identified survey. Counted once per survey and scope.
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
                await _queries.CountByGroupAsync(survey.ItemId, completedOnly: true, cancellationToken));
        }

        return attributes;
    }

    /// <summary>
    /// Lists the groups (collector, version, context kind) of a survey whose real responses every list leaves out
    /// (<see cref="SurveyAnonymousAttributes.Hides"/>), among the groups its responses of any status are in, so a list's
    /// query, count and pages leave them out (<see cref="Indexes.SurveyResponseGroupFilter"/>). Empty for an identified
    /// survey, or when the small groups are large enough together to hide in.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The groups, or <see langword="null"/> when there are more than one query can leave out
    /// (<see cref="Indexes.SurveyResponseGroupFilter.MaxGroups"/>), so the caller lists none of the survey's responses.</returns>
    public async Task<IReadOnlyList<SurveyResponseGroup>> HiddenGroupsAsync(Survey survey, CancellationToken cancellationToken = default)
    {
        var attributes = await AttributesAsync(survey, cancellationToken);

        if (attributes.MinimumGroupSize == 0 || attributes.OtherIsLarge)
        {
            return [];
        }

        if (!_hiddenGroups.TryGetValue(survey.ItemId, out var hidden))
        {
            var groups = (await _queries.CountByGroupAsync(survey.ItemId, completedOnly: false, cancellationToken)).Keys
                .Where(attributes.Hides)
                .ToList();

            _hiddenGroups[survey.ItemId] = hidden = groups.Count > Indexes.SurveyResponseGroupFilter.MaxGroups ? null : groups;
        }

        return hidden;
    }

    /// <summary>
    /// Lists the groups (collector, version, context kind) of a survey whose real responses an output with some narrowing
    /// filters leaves out. Without a filter that names a collector, a version or a context kind, they are the hidden groups
    /// (<see cref="HiddenGroupsAsync"/>): a masked row shows its attributes as "Other", which hides it among the other
    /// small groups. With such a filter, every group that doesn't show as itself (<see cref="SurveyAnonymousAttributes.Shows"/>)
    /// is left out too, masked or hidden: the filter would tell a masked row's real collector, version or context kind
    /// (filter on collector A, and the one row with no version is the one person who answered version 2 through A).
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="narrowing">The output's narrowing filters, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The groups, empty for an identified survey, or <see langword="null"/> when there are more than one query
    /// can leave out (<see cref="Indexes.SurveyResponseGroupFilter.MaxGroups"/>), so the caller lists none of the survey's
    /// responses.</returns>
    public async Task<IReadOnlyList<SurveyResponseGroup>> ConcealedGroupsAsync(Survey survey, SurveyResultsScope narrowing, CancellationToken cancellationToken = default)
    {
        var attributes = await AttributesAsync(survey, cancellationToken);

        if (attributes.MinimumGroupSize == 0)
        {
            return [];
        }

        if (!NamesAttribute(narrowing))
        {
            return await HiddenGroupsAsync(survey, cancellationToken);
        }

        if (!_allGroups.TryGetValue(survey.ItemId, out var all))
        {
            _allGroups[survey.ItemId] = all = [.. (await _queries.CountByGroupAsync(survey.ItemId, completedOnly: false, cancellationToken)).Keys];
        }

        // Only the groups the filter selects: the filter itself already leaves out the others.
        var concealed = all
            .Where(group => Selects(narrowing, group) && !attributes.Shows(group))
            .ToList();

        return concealed.Count > Indexes.SurveyResponseGroupFilter.MaxGroups ? null : concealed;
    }

    private static bool NamesAttribute(SurveyResultsScope narrowing)
        => narrowing is not null
            && (!string.IsNullOrEmpty(narrowing.CollectorId) || narrowing.VersionNumbers is { Count: > 0 } || !string.IsNullOrEmpty(narrowing.ContextKind));

    private static bool Selects(SurveyResultsScope narrowing, SurveyResponseGroup group)
        => (string.IsNullOrEmpty(narrowing.CollectorId) || string.Equals(group.CollectorId, narrowing.CollectorId, StringComparison.Ordinal))
            && (narrowing.VersionNumbers is not { Count: > 0 } || narrowing.VersionNumbers.Contains(group.VersionNumber))
            && (string.IsNullOrEmpty(narrowing.ContextKind) || string.Equals(group.ContextKind, narrowing.ContextKind, StringComparison.Ordinal));

    private static SurveyResultsScope RealOf(Survey survey, SurveyResultsScope narrowing, IReadOnlyList<SurveyResponseGroup> concealed)
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
            HiddenGroups = [.. concealed],
        };
}
