using System.Globalization;
using System.Security.Claims;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.ServiceLevels.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ServiceLevels.AI;

/// <summary>
/// Describes service-level timers for the AI tools: the record each one measures (title and link from the record's own
/// module), the names of its policy, metric and priority, and its times in the user's or the site's time zone.
/// </summary>
internal sealed class ServiceLevelTimerDescriber
{
    private readonly ILocalClock _localClock;
    private readonly IServiceLevelPolicyManager _policies;
    private readonly IServiceLevelScopeProvider[] _scopes;
    private readonly IRecordDirectory _records;
    private readonly IStringLocalizerFactory _localizerFactory;
    private readonly Dictionary<string, ServiceLevelPolicy> _policyCache = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (Dictionary<string, string> Metrics, Dictionary<string, string> Priorities)> _scopeNames = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceLevelTimerDescriber"/> class.
    /// </summary>
    /// <param name="services">The services of the tool call.</param>
    public ServiceLevelTimerDescriber(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        _localClock = services.GetRequiredService<ILocalClock>();
        _policies = services.GetRequiredService<IServiceLevelPolicyManager>();
        _scopes = [.. services.GetServices<IServiceLevelScopeProvider>()];
        _records = services.GetService<IRecordDirectory>();
        _localizerFactory = services.GetService<IStringLocalizerFactory>();
    }

    /// <summary>
    /// Reads the titles and links of the records the user may see, with one lookup per kind. A record's title is shown
    /// only when its kind's access provider grants that record to the user, also to those who manage service level
    /// policies: they see every timer, but the record sources describe a record without asking who reads it, so a title
    /// could name a draft or a record the user may not open. The Service-level timers report shows only the kind and id.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="providers">The access providers by kind.</param>
    /// <param name="subjects">The records.</param>
    /// <param name="cancellationToken">A token to cancel the lookup.</param>
    /// <returns>The summaries of the records found that the user may see.</returns>
    public async Task<IReadOnlyDictionary<RecordRef, RecordSummary>> GetSummariesAsync(
        ClaimsPrincipal user,
        IReadOnlyDictionary<string, IServiceLevelTimerAccessProvider> providers,
        IEnumerable<RecordRef> subjects,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(providers);

        var granted = new List<RecordRef>();

        foreach (var kind in subjects.Where(subject => !subject.IsEmpty).Distinct().GroupBy(subject => subject.Kind, StringComparer.Ordinal))
        {
            if (!providers.TryGetValue(kind.Key, out var provider))
            {
                continue;
            }

            var visible = await provider.GetVisibleAsync(user, [.. kind.Select(subject => subject.Id)], cancellationToken);

            granted.AddRange(kind.Where(subject => visible?.Contains(subject.Id) == true));
        }

        if (_records is null || granted.Count == 0)
        {
            return new Dictionary<RecordRef, RecordSummary>();
        }

        var summaries = await _records.GetSummariesAsync(granted, cancellationToken);

        // Only what was asked for, should a record directory answer more.
        return granted.Where(summaries.ContainsKey).ToDictionary(subject => subject, subject => summaries[subject]);
    }

    /// <summary>
    /// Describes the record a timer measures.
    /// </summary>
    /// <param name="subject">The record.</param>
    /// <param name="summaries">The summaries read with <see cref="GetSummariesAsync"/>.</param>
    /// <returns>The record's kind, id, title and link.</returns>
    public object DescribeRecord(RecordRef subject, IReadOnlyDictionary<RecordRef, RecordSummary> summaries)
    {
        var summary = summaries.GetValueOrDefault(subject);

        return new
        {
            kind = subject.Kind,
            kindName = KindName(subject.Kind),
            id = subject.Id,
            title = summary?.Title,
            link = summary?.AdminUrl,
        };
    }

    /// <summary>
    /// Gets the name a kind of record is shown by.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The localized name, or the kind itself.</returns>
    public string KindName(string kind)
        => _records?.GetSource(kind)?.DisplayName is { } displayName && _localizerFactory is not null
            ? _localizerFactory.Localize(displayName).Value
            : kind;

    /// <summary>
    /// Gets the names of a timer's policy, metric and priority.
    /// </summary>
    /// <param name="timer">The timer.</param>
    /// <param name="cancellationToken">A token to cancel the lookup.</param>
    /// <returns>The names; a metric or priority the policy's scope does not name keeps its key.</returns>
    public async Task<(string Policy, string Metric, string Priority)> GetNamesAsync(ServiceLevelTimer timer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(timer);

        var policy = await FindPolicyAsync(timer.PolicyId, cancellationToken);
        var (metrics, priorities) = await GetScopeNamesAsync(policy?.Scope, cancellationToken);

        return (
            policy?.Name,
            timer.Metric is { } metric && metrics.TryGetValue(metric, out var metricName) ? metricName : timer.Metric,
            timer.Priority is { Length: > 0 } priority && priorities.TryGetValue(priority, out var priorityName) ? priorityName : null);
    }

    /// <summary>
    /// Converts a stored UTC time to the user's or the site's time zone.
    /// </summary>
    /// <param name="utc">The UTC time.</param>
    /// <returns>The local time with its offset, or <see langword="null"/>.</returns>
    public async Task<string> ToLocalAsync(DateTime? utc)
    {
        if (utc is null || utc.Value == default)
        {
            return null;
        }

        var local = await _localClock.ConvertToLocalAsync(DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc));

        return local.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Gives a number of seconds of a timer's hours as minutes, rounded to one decimal.
    /// </summary>
    /// <param name="seconds">The seconds.</param>
    /// <returns>The minutes, or <see langword="null"/>.</returns>
    public static double? Minutes(long? seconds)
        => seconds is { } value ? Math.Round(value / 60d, 1) : null;

    private async Task<ServiceLevelPolicy> FindPolicyAsync(string policyId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(policyId))
        {
            return null;
        }

        if (!_policyCache.TryGetValue(policyId, out var policy))
        {
            policy = await _policies.FindByIdAsync(policyId, cancellationToken);
            _policyCache[policyId] = policy;
        }

        return policy;
    }

    private async Task<(Dictionary<string, string> Metrics, Dictionary<string, string> Priorities)> GetScopeNamesAsync(string scope, CancellationToken cancellationToken)
    {
        var key = scope ?? string.Empty;

        if (_scopeNames.TryGetValue(key, out var names))
        {
            return names;
        }

        var provider = _scopes.FirstOrDefault(item => string.Equals(item.Name, scope, StringComparison.OrdinalIgnoreCase));
        var metrics = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var priorities = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (provider is not null)
        {
            foreach (var option in await provider.GetMetricsAsync(cancellationToken))
            {
                metrics.TryAdd(option.Key, option.Name);
            }

            foreach (var option in await provider.GetPrioritiesAsync(cancellationToken))
            {
                if (!string.IsNullOrEmpty(option.Key))
                {
                    priorities.TryAdd(option.Key, option.Name);
                }
            }
        }

        names = (metrics, priorities);
        _scopeNames[key] = names;

        return names;
    }
}
