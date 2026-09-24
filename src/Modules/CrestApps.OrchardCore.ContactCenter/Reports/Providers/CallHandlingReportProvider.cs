using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Providers;

/// <summary>
/// Call handling as the event log records it, per agent and per queue: talk time with hold taken out, hold, ring
/// time, the wait each call really had in queue, and abandons counted as abandons.
/// </summary>
/// <remarks>
/// The interaction-based reports compute talk time as the span from answer to end, which includes every hold, and read
/// timestamps a re-offered call overwrites. This report reads the call's own recorded changes instead: the agent's leg
/// answering and the call ending, each hold and resume, each offer's ring, and each queue's measured wait.
/// </remarks>
public sealed class CallHandlingReportProvider : ContactCenterReportBase
{
    private readonly IInteractionEventStore _eventStore;
    private readonly IAgentProfileManager _agentManager;
    private readonly IActivityQueueManager _queueManager;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="CallHandlingReportProvider"/> class.
    /// </summary>
    /// <param name="reportingService">The Contact Center reporting service.</param>
    /// <param name="capabilityGuard">The guard that decides whether the producing capabilities are enabled.</param>
    /// <param name="eventStore">The event log.</param>
    /// <param name="agentManager">The agent directory, for agent names.</param>
    /// <param name="queueManager">The queues, for queue names.</param>
    /// <param name="clock">The clock, so a call still connected is counted up to now and no further.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CallHandlingReportProvider(
        IContactCenterReportingService reportingService,
        IContactCenterReportCapabilityGuard capabilityGuard,
        IInteractionEventStore eventStore,
        IAgentProfileManager agentManager,
        IActivityQueueManager queueManager,
        IClock clock,
        IStringLocalizer<CallHandlingReportProvider> stringLocalizer)
        : base(reportingService, capabilityGuard, stringLocalizer)
    {
        _eventStore = eventStore;
        _agentManager = agentManager;
        _queueManager = queueManager;
        _clock = clock;
    }

    /// <inheritdoc/>
    public override string Name => "contact-center-call-handling";

    /// <inheritdoc/>
    public override LocalizedString DisplayName => S["Talk, hold, ring and queue wait"];

    /// <inheritdoc/>
    public override LocalizedString Description => S["Talk time with hold taken out, hold, ring time and missed offers by agent, and the real queue wait and abandons by queue, from the calls' recorded changes."];

    /// <inheritdoc/>
    public override string Category => ReportsConstants.Categories.AgentPerformance;

    /// <inheritdoc/>
    public override IReadOnlyCollection<string> FilterNames { get; } =
    [
        ContactCenterReportFilter.QueueId,
        ContactCenterReportFilter.AgentId,
    ];

    /// <inheritdoc/>
    /// <remarks>Call, leg and hold changes are recorded only for voice calls, which only the Voice capability carries.</remarks>
    public override IReadOnlyCollection<string> RequiredFeatureIds { get; } =
    [
        ContactCenterConstants.Feature.Voice,
    ];

    /// <inheritdoc/>
    protected override async Task<ReportDocument> RunCoreAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var range = context.Filter.GetDateRange();
        var fromUtc = range.FromUtc.GetValueOrDefault();
        var toUtc = ContactCenterReportPeriod.ObservedEnd(range.ToUtc, _clock.UtcNow);
        var criteria = ContactCenterReportFilter.GetCriteria(context.Filter);

        var callEvents = await _eventStore.GetByAggregateWindowAsync(nameof(Interaction), CallHandlingMetrics.CallEventTypes, null, fromUtc, toUtc, cancellationToken);
        var offerEvents = await _eventStore.GetByAggregateWindowAsync(nameof(ActivityReservation), CallHandlingMetrics.OfferEventTypes, null, fromUtc, toUtc, cancellationToken);
        var metrics = CallHandlingMetrics.Calculate(callEvents, offerEvents, toUtc, criteria.AgentId, criteria.QueueId);

        var agents = (await _agentManager.GetAllAsync(cancellationToken))
            .Where(agent => !string.IsNullOrEmpty(agent.ItemId))
            .ToDictionary(agent => agent.ItemId, agent => agent.UserName, StringComparer.Ordinal);
        var queues = (await _queueManager.GetAllAsync(cancellationToken))
            .Where(queue => !string.IsNullOrEmpty(queue.ItemId))
            .ToDictionary(queue => queue.ItemId, queue => queue.Name, StringComparer.Ordinal);

        return Build(
            metrics,
            agentId => ReportValue.UserDisplayName(agentId is not null && agents.TryGetValue(agentId, out var userName) ? userName : null, S["(Unknown agent)"].Value),
            queueId => string.IsNullOrEmpty(queueId)
                ? S["(No queue)"].Value
                : queues.TryGetValue(queueId, out var name) && !string.IsNullOrEmpty(name) ? name : queueId);
    }

    /// <summary>
    /// Renders the metrics.
    /// </summary>
    /// <param name="metrics">The measured call handling.</param>
    /// <param name="agentName">Resolves an agent's display name.</param>
    /// <param name="queueName">Resolves a queue's display name.</param>
    /// <returns>The report document.</returns>
    internal ReportDocument Build(CallHandlingMetrics metrics, Func<string, string> agentName, Func<string, string> queueName)
    {
        var agents = metrics.Agents;
        var queues = metrics.Queues;
        var handled = agents.Sum(agent => agent.CallsHandled);
        var talk = agents.Sum(agent => agent.TalkSeconds);
        var hold = agents.Sum(agent => agent.HeldSeconds);
        var accepted = agents.Sum(agent => agent.OffersAccepted);
        var answeredFromQueue = queues.Sum(queue => queue.AnsweredFromQueue);

        var document = new ReportDocument()
            .Add(ReportSection.ForMetrics(S["Summary"].Value,
            [
                new ReportMetric(S["Calls handled"].Value, ReportFormat.Number(handled)),
                new ReportMetric(S["Talk time (excluding hold)"].Value, AuditReportFormat.Clock(talk), S["Average {0}", ReportFormat.Duration(Average(talk, handled))].Value),
                new ReportMetric(S["Hold time"].Value, AuditReportFormat.Clock(hold), S["Average {0} per call", ReportFormat.Duration(Average(hold, handled))].Value),
                new ReportMetric(S["Average ring to answer"].Value, ReportFormat.Duration(Average(agents.Sum(agent => agent.RingToAnswerSeconds), accepted))),
                new ReportMetric(S["Missed offers"].Value, ReportFormat.Number(agents.Sum(agent => agent.OffersMissed)), S["{0} declined", agents.Sum(agent => agent.OffersDeclined)].Value),
                new ReportMetric(S["Average queue wait"].Value, ReportFormat.Duration(Average(queues.Sum(queue => queue.AnsweredWaitSeconds), answeredFromQueue))),
                new ReportMetric(S["Abandoned"].Value, ReportFormat.Number(metrics.Abandoned)),
            ]));

        if (agents.Count > 0)
        {
            var columns = new[]
            {
                new ReportColumn(S["Agent"].Value),
                new ReportColumn(S["Calls"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Connected"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Hold"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Holds"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Talk (excl. hold)"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Avg talk"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Offers"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Accepted"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Declined"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Missed"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Cancelled"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Ring time"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Avg ring to answer"].Value, ReportColumnAlign.End),
            };

            var rows = agents
                .OrderByDescending(agent => agent.CallsHandled)
                .ThenBy(agent => agentName(agent.AgentId), StringComparer.OrdinalIgnoreCase)
                .Select(agent => new ReportRow(
                [
                    agentName(agent.AgentId),
                    ReportFormat.Number(agent.CallsHandled),
                    AuditReportFormat.Clock(agent.ConnectedSeconds),
                    AuditReportFormat.Clock(agent.HeldSeconds),
                    ReportFormat.Number(agent.Holds),
                    AuditReportFormat.Clock(agent.TalkSeconds),
                    ReportFormat.Duration(Average(agent.TalkSeconds, agent.CallsHandled)),
                    ReportFormat.Number(agent.OffersPresented),
                    ReportFormat.Number(agent.OffersAccepted),
                    ReportFormat.Number(agent.OffersDeclined),
                    ReportFormat.Number(agent.OffersMissed),
                    ReportFormat.Number(agent.OffersCancelled),
                    AuditReportFormat.Clock(agent.RingSeconds),
                    ReportFormat.Duration(Average(agent.RingToAnswerSeconds, agent.OffersAccepted)),
                ]))
                .ToList();

            rows.Add(new ReportRow(
            [
                S["All agents"].Value,
                ReportFormat.Number(handled),
                AuditReportFormat.Clock(agents.Sum(agent => agent.ConnectedSeconds)),
                AuditReportFormat.Clock(hold),
                ReportFormat.Number(agents.Sum(agent => agent.Holds)),
                AuditReportFormat.Clock(talk),
                ReportFormat.Duration(Average(talk, handled)),
                ReportFormat.Number(agents.Sum(agent => agent.OffersPresented)),
                ReportFormat.Number(accepted),
                ReportFormat.Number(agents.Sum(agent => agent.OffersDeclined)),
                ReportFormat.Number(agents.Sum(agent => agent.OffersMissed)),
                ReportFormat.Number(agents.Sum(agent => agent.OffersCancelled)),
                AuditReportFormat.Clock(agents.Sum(agent => agent.RingSeconds)),
                ReportFormat.Duration(Average(agents.Sum(agent => agent.RingToAnswerSeconds), accepted)),
            ], ReportRowKind.GrandTotal));

            document.Add(ReportSection.ForTable(S["By agent"].Value, columns, rows));
        }

        if (queues.Count > 0)
        {
            var columns = new[]
            {
                new ReportColumn(S["Queue"].Value),
                new ReportColumn(S["Queued"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Answered"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Abandoned"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Abandon rate"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Avg wait to answer"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Avg wait before abandon"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Longest wait"].Value, ReportColumnAlign.End),
            };

            var rows = queues
                .OrderByDescending(queue => queue.Queued)
                .ThenBy(queue => queueName(queue.QueueId), StringComparer.OrdinalIgnoreCase)
                .Select(queue => new ReportRow(
                [
                    queueName(queue.QueueId),
                    ReportFormat.Number(queue.Queued),
                    ReportFormat.Number(queue.AnsweredFromQueue),
                    ReportFormat.Number(queue.Abandoned),
                    ReportFormat.Percent(queue.AbandonRate),
                    ReportFormat.Duration(Average(queue.AnsweredWaitSeconds, queue.AnsweredFromQueue)),
                    ReportFormat.Duration(Average(queue.AbandonedWaitSeconds, queue.Abandoned)),
                    ReportFormat.Duration(queue.LongestWaitSeconds),
                ]))
                .ToList();

            document.Add(ReportSection.ForTable(S["By queue"].Value, columns, rows));
        }

        return document;
    }

    private static double Average(double total, int count)
        => count > 0 ? total / count : 0;
}
