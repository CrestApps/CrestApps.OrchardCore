using CrestApps.OrchardCore.Surveys.Core.Logic;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Surveys.Core.Runtime;

/// <summary>
/// Counts completed responses in the quotas they match and screens out respondents whose group is full. Counts are
/// changed only under the survey's completion lock, which the response service holds.
/// </summary>
public sealed class SurveyQuotaService : ISurveyQuotaService
{
    private readonly SurveyCounterStore _store;
    private readonly SurveyLogicEngine _logic;
    private readonly IEnumerable<ISurveyQuotaObserver> _observers;
    private readonly IServiceProvider _serviceProvider;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyQuotaService"/> class.
    /// </summary>
    /// <param name="store">The counter store.</param>
    /// <param name="logic">The logic engine.</param>
    /// <param name="observers">The quota observers.</param>
    /// <param name="serviceProvider">The service provider, for the survey manager that closes surveys.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public SurveyQuotaService(
        SurveyCounterStore store,
        SurveyLogicEngine logic,
        IEnumerable<ISurveyQuotaObserver> observers,
        IServiceProvider serviceProvider,
        IClock clock,
        ILogger<SurveyQuotaService> logger)
    {
        _store = store;
        _logic = logic;
        _observers = observers;
        _serviceProvider = serviceProvider;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<SurveyQuota> FindFullAsync(Survey survey, SurveyVersion version, SurveyResponse response, SurveyLogicState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(survey);
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(response);

        var quotas = Active(version.Definition).Where(quota => quota.Action == QuotaAction.ScreenOut).ToList();

        if (quotas.Count == 0)
        {
            return null;
        }

        var counts = await GetCountsAsync(survey.ItemId, cancellationToken);

        return quotas.FirstOrDefault(quota => (counts.TryGetValue(quota.Id, out var count) ? count : 0) >= quota.Limit
            && Matches(version, response, state, quota));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<(SurveyQuota Quota, int Count)>> RecordAsync(Survey survey, SurveyVersion version, SurveyResponse response, SurveyLogicState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(survey);
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(response);

        var matching = Active(version.Definition).Where(quota => Matches(version, response, state, quota)).ToList();

        if (matching.Count == 0)
        {
            return [];
        }

        var counter = await _store.FindAsync(survey.ItemId, SurveyCounterKinds.Quotas, cancellationToken);
        var isNew = counter is null;

        counter ??= new SurveyCounter
        {
            ItemId = IdGenerator.GenerateId(),
            SurveyId = survey.ItemId,
            Kind = SurveyCounterKinds.Quotas,
        };

        var reached = new List<(SurveyQuota Quota, int Count)>();

        foreach (var quota in matching)
        {
            var count = (counter.Counts.TryGetValue(quota.Id, out var current) ? current : 0) + 1;
            counter.Counts[quota.Id] = count;

            if (count == quota.Limit)
            {
                reached.Add((quota, count));
            }
        }

        counter.ModifiedUtc = _clock.UtcNow;

        if (isNew)
        {
            await _store.CreateAsync(counter, cancellationToken);
        }
        else
        {
            await _store.UpdateAsync(counter, cancellationToken);
        }

        return reached;
    }

    /// <inheritdoc />
    public async Task NotifyReachedAsync(Survey survey, IReadOnlyList<(SurveyQuota Quota, int Count)> reached, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(survey);

        if (reached is null || reached.Count == 0)
        {
            return;
        }

        foreach (var (quota, count) in reached)
        {
            foreach (var observer in _observers)
            {
                try
                {
                    await observer.QuotaReachedAsync(survey, quota, count);
                }
                catch (Exception exception) when (!exception.IsFatal())
                {
                    _logger.LogError(exception, "A survey quota observer failed for the survey '{SurveyId}'.", survey.ItemId);
                }
            }
        }

        if (reached.Any(item => item.Quota.Action == QuotaAction.CloseSurvey) && _serviceProvider.GetService<ISurveyManager>() is { } manager)
        {
            await manager.ChangeStatusAsync(survey.ItemId, SurveyStatus.Closed, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, int>> GetCountsAsync(string surveyId, CancellationToken cancellationToken = default)
    {
        var counter = await _store.FindAsync(surveyId, SurveyCounterKinds.Quotas, cancellationToken);

        return counter is null
            ? new Dictionary<string, int>(StringComparer.Ordinal)
            : new Dictionary<string, int>(counter.Counts, StringComparer.Ordinal);
    }

    /// <summary>
    /// Gets the quotas of a definition that count: a limit above zero.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <returns>The quotas.</returns>
    public static IEnumerable<SurveyQuota> Active(SurveyDefinition definition)
        => (definition?.Quotas ?? []).Where(quota => quota is not null && !string.IsNullOrEmpty(quota.Id) && quota.Limit > 0);

    private bool Matches(SurveyVersion version, SurveyResponse response, SurveyLogicState state, SurveyQuota quota)
        => _logic.Holds(version.Definition, response, state, quota.Rule, null, SurveyRuntimeVersions.CacheKey(version));
}
