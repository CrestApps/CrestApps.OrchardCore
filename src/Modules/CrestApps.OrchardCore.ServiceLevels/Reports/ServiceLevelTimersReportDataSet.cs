using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using CrestApps.OrchardCore.ServiceLevels.Core.Indexes;
using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.ServiceLevels.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using OrchardCore.Localization;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.ServiceLevels.Reports;

/// <summary>
/// The Service Levels data source: the timers that track each record's targets.
/// </summary>
internal sealed class ServiceLevelReportDataSource : RecordDataSource, IDataSource
{
    public const string SourceName = "ServiceLevels";

    public const string TimersDataSet = "ServiceLevelTimers";

    private readonly IRecordDataSet[] _dataSets;

    public ServiceLevelReportDataSource(ServiceLevelTimersReportDataSet timers)
    {
        _dataSets = [timers];
    }

    public static string Name => SourceName;

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<ServiceLevelReportDataSource>("Service levels");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<ServiceLevelReportDataSource>("The service-level timers of the records you can see: each target, when it was due, and whether it was met or breached.");

    protected override IEnumerable<IRecordDataSet> DataSets => _dataSets;
}

/// <summary>
/// One row per service-level timer the reader may see. Those who manage policies see every timer; others see the timers
/// of records their module's access provider lets them see.
/// </summary>
internal sealed class ServiceLevelTimersReportDataSet : IndexedReportDataSet<ServiceLevelTimer, ServiceLevelTimerIndex>
{
    // How many rows are checked against the access providers at a time.
    private const int BatchSize = 200;

    private readonly IAuthorizationService _authorizationService;
    private readonly IEnumerable<IServiceLevelTimerAccessProvider> _providers;

    public ServiceLevelTimersReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IEnumerable<IServiceLevelTimerAccessProvider> providers,
        IStringLocalizer<ServiceLevelTimersReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                ServiceLevelReportDataSource.TimersDataSet,
                stringLocalizer["Service-level timers"],
                stringLocalizer["One row per target timer of a record you can see, such as a ticket's first response: the policy, the target, when it started and was due, and whether it was met or breached."]),
            session,
            authorizationService,
            ServiceLevelsConstants.CollectionName)
    {
        _authorizationService = authorizationService;
        _providers = providers;

        var S = stringLocalizer;
        var timer = S["Timer"].Value;
        var dates = S["Dates"].Value;
        var outcome = S["Outcome"].Value;

        AddField("TimerId", S["Timer ID"], DataFieldType.Text, record => record.ItemId, timer);
        AddField("SubjectKind", S["Record kind"], DataFieldType.Text, record => record.Subject.Kind, timer).Description = S["The kind of record the timer is on, such as Ticket or WorkOrder."].Value;
        AddField("SubjectId", S["Record ID"], DataFieldType.Text, record => record.Subject.Id, timer);
        AddField(nameof(ServiceLevelTimer.Metric), S["Metric"], DataFieldType.Text, record => record.Metric, timer).Description = S["What the target measures, such as FirstResponse or Resolution."].Value;
        AddField(nameof(ServiceLevelTimer.PolicyId), S["Policy ID"], DataFieldType.Text, record => record.PolicyId, timer);
        AddField(nameof(ServiceLevelTimer.Priority), S["Priority"], DataFieldType.Text, record => record.Priority, timer);
        AddField(nameof(ServiceLevelTimer.State), S["State"], DataFieldType.Text, record => record.State.ToString(), timer);
        AddField("TargetMinutes", S["Target (minutes)"], DataFieldType.Decimal, record => Math.Round(record.TargetSeconds / 60d, 1), timer).Description = S["The target in business minutes."].Value;

        AddField(nameof(ServiceLevelTimer.StartedUtc), S["Started"], DataFieldType.DateTime, record => record.StartedUtc, dates);
        AddField(nameof(ServiceLevelTimer.WarnAtUtc), S["Warns at"], DataFieldType.DateTime, record => record.WarnAtUtc, dates);
        AddField(nameof(ServiceLevelTimer.DueUtc), S["Due"], DataFieldType.DateTime, record => record.DueUtc, dates);
        AddField(nameof(ServiceLevelTimer.EndedUtc), S["Ended"], DataFieldType.DateTime, record => record.EndedUtc, dates);

        AddField("Breached", S["Breached"], DataFieldType.Boolean, record => record.IsBreached, outcome);
        AddField("Met", S["Met"], DataFieldType.Boolean, record => record.EndedUtc is not null && !record.IsBreached, outcome).Description = S["Ended before it was due."].Value;
        AddField("Warned", S["Warned"], DataFieldType.Boolean, record => record.WarnedUtc is not null, outcome);
        AddField(nameof(ServiceLevelTimer.BreachedUtc), S["Breached at"], DataFieldType.DateTime, record => record.BreachedUtc, outcome);
        AddField("ElapsedMinutes", S["Business minutes used"], DataFieldType.Decimal, record => record.ElapsedSeconds is { } seconds ? Math.Round(seconds / 60d, 1) : null, outcome).Description = S["The business minutes the target took; empty while it is still running."].Value;

        CompleteDescription();
    }

    protected override bool CanAggregate => false;

    protected override (string Field, Expression<Func<ServiceLevelTimerIndex, DateTime>> Column)? DateColumn => (nameof(ServiceLevelTimer.StartedUtc), index => index.StartedUtc);

    protected override IReadOnlyDictionary<string, Expression<Func<ServiceLevelTimerIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<ServiceLevelTimerIndex, string>>>(StringComparer.Ordinal)
        {
            ["SubjectId"] = index => index.SubjectId,
        };

    public override async Task<bool> CanReadAsync(DataSourceContext context, CancellationToken cancellationToken = default)
        => context?.User is { } user && (_providers.Any() || await _authorizationService.AuthorizeAsync(user, ServiceLevelsPermissions.ManageServiceLevelPolicies));

    protected override async IAsyncEnumerable<ServiceLevelTimer> LoadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (query?.Context?.User is not { } user)
        {
            yield break;
        }

        if (await _authorizationService.AuthorizeAsync(user, ServiceLevelsPermissions.ManageServiceLevelPolicies))
        {
            await foreach (var timer in base.LoadAsync(query, cancellationToken))
            {
                yield return timer;
            }

            yield break;
        }

        var providers = _providers
            .GroupBy(provider => provider.SubjectKind, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var known = new Dictionary<(string, string), bool>();
        var buffer = new List<ServiceLevelTimer>(BatchSize);

        await foreach (var timer in base.LoadAsync(query, cancellationToken))
        {
            buffer.Add(timer);

            if (buffer.Count < BatchSize)
            {
                continue;
            }

            foreach (var kept in await FlushAsync(user, providers, known, buffer, cancellationToken))
            {
                yield return kept;
            }
        }

        foreach (var kept in await FlushAsync(user, providers, known, buffer, cancellationToken))
        {
            yield return kept;
        }
    }

    private static async Task<List<ServiceLevelTimer>> FlushAsync(
        ClaimsPrincipal user,
        Dictionary<string, IServiceLevelTimerAccessProvider> providers,
        Dictionary<(string, string), bool> known,
        List<ServiceLevelTimer> buffer,
        CancellationToken cancellationToken)
    {
        foreach (var kind in buffer.GroupBy(timer => timer.Subject.Kind ?? string.Empty, StringComparer.Ordinal))
        {
            var unknown = kind.Select(timer => timer.Subject.Id).Where(id => !string.IsNullOrEmpty(id) && !known.ContainsKey((kind.Key, id))).Distinct(StringComparer.Ordinal).ToList();

            if (unknown.Count == 0)
            {
                continue;
            }

            var visible = providers.TryGetValue(kind.Key, out var provider)
                ? await provider.GetVisibleAsync(user, unknown, cancellationToken)
                : null;

            foreach (var id in unknown)
            {
                known[(kind.Key, id)] = visible?.Contains(id) == true;
            }
        }

        var kept = buffer.Where(timer => known.GetValueOrDefault((timer.Subject.Kind ?? string.Empty, timer.Subject.Id ?? string.Empty))).ToList();
        buffer.Clear();

        return kept;
    }
}
