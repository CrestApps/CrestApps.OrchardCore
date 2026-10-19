using CrestApps.OrchardCore.ContactCenter.Core;
using OrchardCore.Localization;
using System.Globalization;
using System.Text.Json;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models.Dispatch;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Providers;

/// <summary>
/// The dialer decision log report: every routing decision and dial suppression of a period, which record, queue,
/// campaign and agents it was about, its outcome and reason, how often each reason came up, and the queues with work
/// due now that routing has stopped deciding for.
/// </summary>
/// <remarks>
/// It reads the decision log's indexed columns to filter and count, and reads the events themselves only for the rows
/// it lists. A routing decision that routing repeated identically is one event that counts every time it was made.
/// </remarks>
public sealed class DialerDecisionLogReportProvider : ContactCenterReportBase, IReport
{
    /// <summary>
    /// The most decisions listed one by one on screen.
    /// </summary>
    internal const int DisplayRowLimit = 500;

    /// <summary>
    /// The most decisions an export lists one by one.
    /// </summary>
    internal const int ExportRowLimit = 10_000;

    private readonly IDialerDecisionLog _decisionLog;
    private readonly IDialerDispatchDiagnosticsService _dispatchDiagnostics;
    private readonly IQueueItemManager _queueItemManager;
    private readonly IActivityQueueManager _queueManager;
    private readonly ICatalogManager<OmnichannelCampaign> _campaignManager;
    private readonly IAgentProfileStore _agentProfileStore;
    private readonly UserManager<IUser> _userManager;
    private readonly IClock _clock;
    private readonly ContactCenterReportingOptions _reportingOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialerDecisionLogReportProvider"/> class.
    /// </summary>
    /// <param name="reportingService">The Contact Center reporting service.</param>
    /// <param name="capabilityGuard">The guard that decides whether the producing capabilities are enabled.</param>
    /// <param name="decisionLog">The dialer decision log.</param>
    /// <param name="dispatchDiagnostics">The service that says which queues have work due now.</param>
    /// <param name="queueItemManager">The queue items, for how many wait in each queue.</param>
    /// <param name="queueManager">The stored queues, for queue names.</param>
    /// <param name="campaignManager">The campaigns, for campaign names.</param>
    /// <param name="agentProfileStore">The agent directory, for agent names.</param>
    /// <param name="userManager">The user manager, for the name of an agent whose profile does not carry one.</param>
    /// <param name="clock">The clock, for how long a queue has gone without a decision.</param>
    /// <param name="reportingOptions">The reporting options, for how long is too long without a decision.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DialerDecisionLogReportProvider(
        IContactCenterReportingService reportingService,
        IContactCenterReportCapabilityGuard capabilityGuard,
        IDialerDecisionLog decisionLog,
        IDialerDispatchDiagnosticsService dispatchDiagnostics,
        IQueueItemManager queueItemManager,
        IActivityQueueManager queueManager,
        ICatalogManager<OmnichannelCampaign> campaignManager,
        IAgentProfileStore agentProfileStore,
        UserManager<IUser> userManager,
        IClock clock,
        IOptions<ContactCenterReportingOptions> reportingOptions,
        IStringLocalizer<DialerDecisionLogReportProvider> stringLocalizer)
        : base(reportingService, capabilityGuard, stringLocalizer)
    {
        _decisionLog = decisionLog;
        _dispatchDiagnostics = dispatchDiagnostics;
        _queueItemManager = queueItemManager;
        _queueManager = queueManager;
        _campaignManager = campaignManager;
        _agentProfileStore = agentProfileStore;
        _userManager = userManager;
        _clock = clock;
        _reportingOptions = reportingOptions.Value;
    }

    /// <inheritdoc/>
    public static string Name => "contact-center-dialer-decision-log";

    /// <inheritdoc/>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<DialerDecisionLogReportProvider>("Dialer decision log");

    /// <inheritdoc/>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<DialerDecisionLogReportProvider>("Every routing decision and dial suppression: the activity, queue, campaign and agents it was about, the outcome and the reason, counts by reason, and queues with work due now that routing has gone quiet on.");

    /// <inheritdoc/>
    public static string Category => ReportsConstants.Categories.QueueRouting;

    /// <inheritdoc/>
    public static Permission Permission => ContactCenterPermissions.ViewReports;

    /// <inheritdoc/>
    public override IReadOnlyCollection<string> FilterNames { get; } =
    [
        ContactCenterReportFilter.CampaignId,
        ContactCenterReportFilter.QueueId,
        ContactCenterReportFilter.AgentId,
        ContactCenterReportFilter.ActivityItemId,
        ContactCenterReportFilter.DecisionReason,
    ];

    /// <inheritdoc/>
    /// <remarks>
    /// Routing decisions are recorded by Work Distribution, which the reports already need, and suppressions by the
    /// outbound dialer; a tenant without the dialer simply has no suppressions to list.
    /// </remarks>
    public override IReadOnlyCollection<string> RequiredFeatureIds { get; } = [];

    /// <inheritdoc/>
    protected override async Task<ReportDocument> RunCoreAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var range = context.Filter.GetDateRange();
        var query = new DialerDecisionLogQuery
        {
            FromUtc = range.FromUtc,
            ToUtc = range.ToUtc,
            QueueId = ContactCenterReportFilter.GetString(context.Filter, ContactCenterReportFilter.QueueId),
            CampaignId = ContactCenterReportFilter.GetString(context.Filter, ContactCenterReportFilter.CampaignId),
            AgentId = ContactCenterReportFilter.GetString(context.Filter, ContactCenterReportFilter.AgentId),
            ActivityItemId = Trimmed(ContactCenterReportFilter.GetString(context.Filter, ContactCenterReportFilter.ActivityItemId)),
            Reason = ContactCenterReportFilter.GetString(context.Filter, ContactCenterReportFilter.DecisionReason),
        };

        var entries = await _decisionLog.GetEntriesAsync(query, cancellationToken);
        var limit = context.Mode == ReportRunMode.Export
            ? ExportRowLimit
            : DisplayRowLimit;
        var events = await _decisionLog.ListAsync(query, limit, cancellationToken);

        var queues = (await _queueManager.GetAllAsync(cancellationToken))
            .Where(queue => !string.IsNullOrEmpty(queue.ItemId))
            .GroupBy(queue => queue.ItemId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var campaigns = (await _campaignManager.GetAllAsync(cancellationToken))
            .Where(campaign => !string.IsNullOrEmpty(campaign.ItemId))
            .GroupBy(campaign => campaign.ItemId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var rows = events.Select(Describe).Where(row => row is not null).ToArray();
        var agentNames = await ResolveAgentNamesAsync(rows, cancellationToken);

        string CampaignName(string campaignId)
        {
            if (string.IsNullOrEmpty(campaignId))
            {
                return string.Empty;
            }

            return campaigns.TryGetValue(campaignId, out var campaign) && !string.IsNullOrEmpty(campaign.DisplayText)
                ? campaign.DisplayText
                : campaignId;
        }

        string QueueName(string queueId)
        {
            if (ContactCenterConstants.CampaignQueue.GetCampaignId(queueId) is { } campaignId)
            {
                return S["Campaign: {0}", CampaignName(campaignId)].Value;
            }

            return ContactCenterReportNames.Queue(queueId, queues, S);
        }

        // The agent column is named the way every report names an agent; a candidate is named inside the list of
        // candidates, where only the plain user name fits.
        string AgentCell(string agentId)
        {
            if (string.IsNullOrEmpty(agentId))
            {
                return string.Empty;
            }

            return ReportValue.UserDisplayName(agentNames.GetValueOrDefault(agentId), S["(Unknown agent)"].Value);
        }

        string AgentName(string agentId)
            => !string.IsNullOrEmpty(agentId) && agentNames.TryGetValue(agentId, out var userName) && !string.IsNullOrEmpty(userName)
                ? userName
                : S["(Unknown agent)"].Value;

        var document = new ReportDocument();
        var routing = entries.Where(entry => IsRouting(entry.EventType)).ToArray();
        var suppressions = entries.Where(entry => !IsRouting(entry.EventType)).ToArray();

        document.Add(ReportSection.ForMetrics(S["Summary"].Value,
        [
            new ReportMetric(S["Decisions"].Value, ReportFormat.Number(entries.Sum(entry => (long)DialerDecisionReasonSummary.Occurrences(entry)))),
            new ReportMetric(S["Routing decisions"].Value, ReportFormat.Number(routing.Sum(entry => (long)DialerDecisionReasonSummary.Occurrences(entry)))),
            new ReportMetric(S["Assigned"].Value, ReportFormat.Number(routing.Where(entry => entry.Succeeded).Sum(entry => (long)DialerDecisionReasonSummary.Occurrences(entry)))),
            new ReportMetric(S["Not assigned"].Value, ReportFormat.Number(routing.Where(entry => !entry.Succeeded).Sum(entry => (long)DialerDecisionReasonSummary.Occurrences(entry)))),
            new ReportMetric(S["Suppressions"].Value, ReportFormat.Number(suppressions.Length)),
            new ReportMetric(S["Activities"].Value, ReportFormat.Number(entries.Select(entry => entry.ActivityItemId).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).Count())),
        ]));

        var reasons = DialerDecisionReasonSummary.Summarize(entries);

        if (reasons.Count > 0)
        {
            var reasonColumns = new[]
            {
                new ReportColumn(S["Kind"].Value),
                new ReportColumn(S["Reason"].Value),
                new ReportColumn(S["Decisions"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Events"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Last made (UTC)"].Value),
            };

            var reasonRows = reasons
                .Select(row => new ReportRow(
                [
                    DialerDecisionLabels.Kind(row.EventType, S),
                    DialerDecisionLabels.Reason(row.Reason, S),
                    ReportFormat.Number(row.Decisions),
                    ReportFormat.Number(row.Events),
                    FormatUtc(row.LastSeenUtc),
                ]))
                .ToList();

            document.Add(ReportSection.ForTable(S["Decisions by reason"].Value, reasonColumns, reasonRows));
        }

        // How quiet a queue has gone is a question about the queue now, so it is answered when the report looks at
        // queues and campaigns, not at one record, one agent or one reason.
        if (string.IsNullOrEmpty(query.AgentId) && string.IsNullOrEmpty(query.ActivityItemId) && string.IsNullOrEmpty(query.Reason))
        {
            await AddQuietQueuesAsync(document, query, queues.Values, campaigns.Keys, QueueName, cancellationToken);
        }

        var columns = new[]
        {
            new ReportColumn(S["Last made (UTC)"].Value),
            new ReportColumn(S["First made (UTC)"].Value),
            new ReportColumn(S["Times"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Kind"].Value),
            new ReportColumn(S["Queue"].Value),
            new ReportColumn(S["Campaign"].Value),
            new ReportColumn(S["Activity"].Value),
            new ReportColumn(S["Outcome"].Value),
            new ReportColumn(S["Reason"].Value),
            new ReportColumn(S["Agent"].Value),
            new ReportColumn(S["Candidates"].Value),
            new ReportColumn(S["Destination"].Value),
            new ReportColumn(S["Details"].Value),
        };

        var detailRows = rows
            .Select(row => new ReportRow(
            [
                FormatUtc(row.LastSeenUtc),
                FormatUtc(row.OccurredUtc),
                ReportFormat.Number(row.Occurrences),
                DialerDecisionLabels.Kind(row.EventType, S),
                QueueName(row.QueueId),
                CampaignName(row.CampaignId),
                row.ActivityItemId ?? string.Empty,
                row.Suppressed
                    ? S["Suppressed"].Value
                    : row.Succeeded
                        ? S["Assigned"].Value
                        : S["Not assigned"].Value,
                DialerDecisionLabels.Reason(row.Reason, S),
                AgentCell(row.AgentId),
                string.Join("; ", row.Candidates.Select(candidate => DescribeCandidate(candidate, AgentName(candidate.AgentId)))),
                row.Destination ?? string.Empty,
                row.Details ?? string.Empty,
            ]))
            .ToList();

        var title = rows.Length == limit
            ? S["Decisions (the most recent {0})", limit].Value
            : S["Decisions"].Value;

        document.Add(ReportSection.ForTable(title, columns, detailRows));

        return document;
    }

    // The queues routing should be deciding for now, with how long since it last did: each queue with work waiting,
    // and of those, the ones whose work is due now and whose queue is open.
    private async Task AddQuietQueuesAsync(
        ReportDocument document,
        DialerDecisionLogQuery query,
        IEnumerable<ActivityQueue> queues,
        IEnumerable<string> campaignIds,
        Func<string, string> queueName,
        CancellationToken cancellationToken)
    {
        string[] scope;

        if (!string.IsNullOrEmpty(query.QueueId))
        {
            scope = [query.QueueId];
        }
        else if (!string.IsNullOrEmpty(query.CampaignId))
        {
            scope = [ContactCenterConstants.CampaignQueue.CreateId(query.CampaignId)];
        }
        else
        {
            scope = queues
                .Where(queue => queue.Enabled)
                .Select(queue => queue.ItemId)
                .Concat(campaignIds.Select(ContactCenterConstants.CampaignQueue.CreateId))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        var waiting = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var queueId in scope)
        {
            var count = await _queueItemManager.CountWaitingAsync(queueId, cancellationToken);

            if (count > 0)
            {
                waiting[queueId] = count;
            }
        }

        var due = new List<QueueDispatchDiagnostics>();

        if (waiting.Count > 0)
        {
            due.AddRange((await _dispatchDiagnostics.DiagnoseAsync(waiting.Keys.ToArray(), cancellationToken))
                .Where(diagnosis => diagnosis.QueueBlocker is null && diagnosis.DialableNowCount > 0));
        }

        IReadOnlyDictionary<string, DateTime> lastDecisions = new Dictionary<string, DateTime>(StringComparer.Ordinal);

        if (due.Count > 0)
        {
            lastDecisions = await _decisionLog.GetLastRoutingDecisionTimesAsync(
                due.Select(diagnosis => diagnosis.QueueId).ToArray(),
                cancellationToken);
        }

        var now = _clock.UtcNow;
        var threshold = _reportingOptions.DecisionLogQuietThreshold;
        var minutes = Math.Max(1, (int)Math.Round(threshold.TotalMinutes));

        var columns = new[]
        {
            new ReportColumn(S["Queue"].Value),
            new ReportColumn(S["Waiting"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Due now"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Last routing decision (UTC)"].Value),
            new ReportColumn(S["Minutes since"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Status"].Value),
        };

        var rows = new List<ReportRow>();
        var quiet = 0;

        var ordered = due
            .Select(item => (Diagnosis: item, Last: lastDecisions.TryGetValue(item.QueueId, out var lastUtc) ? lastUtc : (DateTime?)null))
            .OrderBy(item => item.Last ?? DateTime.MinValue)
            .ThenBy(item => queueName(item.Diagnosis.QueueId), StringComparer.CurrentCulture);

        foreach (var entry in ordered)
        {
            var isQuiet = entry.Last is not { } lastDecisionUtc || now - lastDecisionUtc > threshold;

            if (isQuiet)
            {
                quiet++;
            }

            rows.Add(new ReportRow(
            [
                queueName(entry.Diagnosis.QueueId),
                ReportFormat.Number(waiting.GetValueOrDefault(entry.Diagnosis.QueueId)),
                ReportFormat.Number(entry.Diagnosis.DialableNowCount),
                entry.Last is { } madeUtc ? FormatUtc(madeUtc) : S["Never"].Value,
                entry.Last is { } sinceUtc ? ReportFormat.Number((long)Math.Floor((now - sinceUtc).TotalMinutes)) : string.Empty,
                isQuiet
                    ? S["No routing decision in the last {0} minutes", minutes].Value
                    : S["Deciding"].Value,
            ]));
        }

        var section = ReportSection.ForTable(S["Queues with work due now"].Value, columns, rows);
        section.Description = S["Routing decides on every pass, about once a minute. A queue with work due now and no routing decision in the last {0} minutes is not being looked at. Quiet queues: {1}.", minutes, quiet].Value;

        document.Add(section);
    }

    // An agent profile created before it carried the user name knows only the user's id, so the name is read from the
    // account instead of reporting a real agent as unknown.
    private async Task<Dictionary<string, string>> ResolveAgentNamesAsync(IEnumerable<DecisionRow> rows, CancellationToken cancellationToken)
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        var agentIds = rows
            .SelectMany(row => row.Candidates.Select(candidate => candidate.AgentId).Append(row.AgentId))
            .Where(agentId => !string.IsNullOrEmpty(agentId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (agentIds.Length == 0)
        {
            return names;
        }

        foreach (var agent in await _agentProfileStore.GetAsync(agentIds, cancellationToken))
        {
            var userName = agent.UserName;

            if (string.IsNullOrEmpty(userName) && !string.IsNullOrEmpty(agent.UserId))
            {
                userName = (await _userManager.FindByIdAsync(agent.UserId))?.UserName;
            }

            names[agent.ItemId] = userName;
        }

        return names;
    }

    private string DescribeCandidate(ActivityRoutingCandidateDecisionData candidate, string name)
    {
        var reasons = candidate.Reasons is null
            ? string.Empty
            : string.Join(", ", candidate.Reasons.Where(reason => !string.IsNullOrWhiteSpace(reason)));

        if (candidate.IsEligible)
        {
            return S["{0}: eligible", name].Value;
        }

        return string.IsNullOrEmpty(reasons)
            ? S["{0}: not eligible", name].Value
            : S["{0}: not eligible ({1})", name, reasons].Value;
    }

    // What one listed decision says, read from its event. A payload this release cannot read is left out of the list;
    // it is still counted above, from its index row.
    internal static DecisionRow Describe(InteractionEvent interactionEvent)
    {
        try
        {
            if (string.Equals(interactionEvent.EventType, ContactCenterConstants.Events.RoutingDecisionMade, StringComparison.Ordinal))
            {
                var data = interactionEvent.GetData<ActivityRoutingDecisionEventData>();

                if (data is null)
                {
                    return null;
                }

                return new DecisionRow
                {
                    EventType = interactionEvent.EventType,
                    OccurredUtc = interactionEvent.OccurredUtc,
                    LastSeenUtc = RoutingDecisionCoalescing.GetLastSeenUtc(interactionEvent, data),
                    Occurrences = RoutingDecisionCoalescing.GetOccurrenceCount(data),
                    QueueId = data.QueueId,
                    CampaignId = ContactCenterConstants.CampaignQueue.GetCampaignId(data.QueueId),
                    ActivityItemId = data.ActivityItemId,
                    Succeeded = data.Succeeded,
                    Reason = data.Reason,
                    AgentId = data.SelectedAgentId,
                    Candidates = data.Candidates?.Where(candidate => candidate is not null).ToArray() ?? [],
                };
            }

            if (string.Equals(interactionEvent.EventType, ContactCenterConstants.Events.DialSuppressed, StringComparison.Ordinal))
            {
                var data = interactionEvent.GetData<DialerSuppressionEventData>();

                if (data is null)
                {
                    return null;
                }

                return new DecisionRow
                {
                    EventType = interactionEvent.EventType,
                    OccurredUtc = interactionEvent.OccurredUtc,
                    LastSeenUtc = interactionEvent.OccurredUtc,
                    Occurrences = 1,
                    QueueId = data.QueueId,
                    CampaignId = data.CampaignId ?? ContactCenterConstants.CampaignQueue.GetCampaignId(data.QueueId),
                    ActivityItemId = data.ActivityItemId ?? interactionEvent.AggregateId,
                    Suppressed = true,
                    Reason = data.Reason.ToString(),
                    AgentId = data.AgentId,
                    Destination = data.Destination,
                    Details = data.Description,
                };
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static bool IsRouting(string eventType)
        => string.Equals(eventType, ContactCenterConstants.Events.RoutingDecisionMade, StringComparison.Ordinal);

    private static string Trimmed(string value)
        => string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();

    private static string FormatUtc(DateTime value)
        => value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    /// <summary>
    /// Represents what one listed decision says.
    /// </summary>
    internal sealed class DecisionRow
    {
        public string EventType { get; init; }

        public DateTime OccurredUtc { get; init; }

        public DateTime LastSeenUtc { get; init; }

        public int Occurrences { get; init; }

        public string QueueId { get; init; }

        public string CampaignId { get; init; }

        public string ActivityItemId { get; init; }

        public bool Succeeded { get; init; }

        public bool Suppressed { get; init; }

        public string Reason { get; init; }

        public string AgentId { get; init; }

        public IReadOnlyList<ActivityRoutingCandidateDecisionData> Candidates { get; init; } = [];

        public string Destination { get; init; }

        public string Details { get; init; }
    }
}
