using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Providers;

/// <summary>
/// An agent's activity in order: every state with when it started, when it ended and how long it lasted, beside the
/// offers, connections and call changes of the same period, each naming who made it.
/// </summary>
/// <remarks>
/// This is the report that answers what an agent was, and was not, doing at any moment, and the one to open for a day
/// the reconciled timecard flags: a missing change and a change superseded by a late sign-off are marked where they
/// happened.
/// </remarks>
public sealed class AgentActivityTimelineReportProvider : ContactCenterReportBase
{
    /// <summary>
    /// The most entries listed, so an unfiltered period over every agent stays readable.
    /// </summary>
    internal const int EntryLimit = 5000;

    private const string FlagColor = "#B42318";

    private readonly IInteractionEventStore _eventStore;
    private readonly IAgentProfileManager _agentManager;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentActivityTimelineReportProvider"/> class.
    /// </summary>
    /// <param name="reportingService">The Contact Center reporting service.</param>
    /// <param name="capabilityGuard">The guard that decides whether the producing capabilities are enabled.</param>
    /// <param name="eventStore">The event log.</param>
    /// <param name="agentManager">The agent directory, for agent names.</param>
    /// <param name="clock">The clock, so a state still held ends now and no later.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AgentActivityTimelineReportProvider(
        IContactCenterReportingService reportingService,
        IContactCenterReportCapabilityGuard capabilityGuard,
        IInteractionEventStore eventStore,
        IAgentProfileManager agentManager,
        IClock clock,
        IStringLocalizer<AgentActivityTimelineReportProvider> stringLocalizer)
        : base(reportingService, capabilityGuard, stringLocalizer)
    {
        _eventStore = eventStore;
        _agentManager = agentManager;
        _clock = clock;
    }

    /// <inheritdoc/>
    public override string Name => "contact-center-agent-activity-timeline";

    /// <inheritdoc/>
    public override LocalizedString DisplayName => S["Agent activity timeline"];

    /// <inheritdoc/>
    public override LocalizedString Description => S["Every agent state, offer, connection and call change in order, with start, end, duration and who made each change."];

    /// <inheritdoc/>
    public override string Category => ReportsConstants.Categories.ComplianceAudit;

    /// <inheritdoc/>
    public override IReadOnlyCollection<string> FilterNames { get; } =
    [
        ContactCenterReportFilter.AgentId,
    ];

    /// <inheritdoc/>
    /// <remarks>Agent state is written by the availability capability the reporting feature already depends on.</remarks>
    public override IReadOnlyCollection<string> RequiredFeatureIds { get; } = [];

    /// <inheritdoc/>
    protected override async Task<ReportDocument> RunCoreAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var range = context.Filter.GetDateRange();
        var fromUtc = range.FromUtc.GetValueOrDefault();
        var toUtc = ContactCenterReportPeriod.ObservedEnd(range.ToUtc, _clock.UtcNow);
        var criteria = ContactCenterReportFilter.GetCriteria(context.Filter);
        var agents = (await _agentManager.GetAllAsync(cancellationToken))
            .Where(agent => !string.IsNullOrEmpty(agent.ItemId))
            .ToDictionary(agent => agent.ItemId, agent => agent.UserName, StringComparer.Ordinal);
        var filtered = !string.IsNullOrEmpty(criteria.AgentId);

        var agentEvents = await AgentStateEventReader.ReadAsync(
            _eventStore,
            filtered ? [criteria.AgentId] : agents.Keys,
            onlyTheseAgents: filtered,
            fromUtc,
            toUtc,
            AgentActivityTimeline.SessionEventTypes,
            cancellationToken);

        var timelines = AgentStateTimeline.Build(agentEvents);
        var sessionEvents = agentEvents
            .Where(interactionEvent => AgentActivityTimeline.SessionEventTypes.Contains(interactionEvent.EventType, StringComparer.Ordinal))
            .ToArray();
        var offerEvents = await OfferEventReader.ReadAsync(_eventStore, fromUtc, toUtc, cancellationToken);

        // Calls are read by the interactions the agents' states and offers name, on the aggregate index, and calls no
        // interaction owns (such as extension calls) by the call session they were recorded against.
        var interactionIds = timelines
            .SelectMany(timeline => timeline.Transitions)
            .Select(transition => transition.InteractionId)
            .Concat(offerEvents.Select(offerEvent => offerEvent.InteractionId))
            .Where(interactionId => !string.IsNullOrEmpty(interactionId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var callEvents = new List<InteractionEvent>();

        if (interactionIds.Length > 0)
        {
            callEvents.AddRange(await _eventStore.GetByAggregateWindowAsync(nameof(Interaction), AgentActivityTimeline.CallEventTypes, interactionIds, fromUtc, toUtc, cancellationToken));
        }

        callEvents.AddRange(await _eventStore.GetByAggregateWindowAsync(nameof(CallSession), AgentActivityTimeline.CallEventTypes, null, fromUtc, toUtc, cancellationToken));

        var entries = AgentActivityTimeline.Build(timelines, sessionEvents, offerEvents, callEvents, fromUtc, toUtc);

        return Build(
            entries,
            agentId => ReportValue.UserDisplayName(agentId is not null && agents.TryGetValue(agentId, out var userName) ? userName : null, S["(Unknown agent)"].Value));
    }

    /// <summary>
    /// Renders the timeline.
    /// </summary>
    /// <param name="entries">The entries, in order.</param>
    /// <param name="agentName">Resolves an agent's display name.</param>
    /// <returns>The report document.</returns>
    internal ReportDocument Build(IReadOnlyList<AgentActivityEntry> entries, Func<string, string> agentName)
    {
        var document = new ReportDocument()
            .Add(ReportSection.ForMetrics(S["Summary"].Value,
            [
                new ReportMetric(S["Entries"].Value, ReportFormat.Number(entries.Count)),
                new ReportMetric(S["State changes"].Value, ReportFormat.Number(entries.Count(entry => entry.Kind == AgentActivityKind.State))),
                new ReportMetric(S["Offers"].Value, ReportFormat.Number(entries.Count(entry => entry.Kind == AgentActivityKind.Offer))),
                new ReportMetric(S["Call changes"].Value, ReportFormat.Number(entries.Count(entry => entry.Kind == AgentActivityKind.Call))),
                new ReportMetric(S["Flagged"].Value, ReportFormat.Number(entries.Count(entry => entry.Flagged))),
            ]));

        var columns = new[]
        {
            new ReportColumn(S["Agent"].Value),
            new ReportColumn(S["Start (UTC)"].Value),
            new ReportColumn(S["End (UTC)"].Value),
            new ReportColumn(S["Duration"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Kind"].Value),
            new ReportColumn(S["Activity"].Value),
            new ReportColumn(S["Detail"].Value),
            new ReportColumn(S["Interaction"].Value),
            new ReportColumn(S["Changed by"].Value),
            new ReportColumn(S["Logged (UTC)"].Value),
        };

        var rows = entries
            .Take(EntryLimit)
            .Select(entry =>
            {
                var row = new ReportRow(
                [
                    agentName(entry.AgentId),
                    AuditReportFormat.Timestamp(entry.StartUtc),
                    entry.EndUtc.HasValue ? AuditReportFormat.Timestamp(entry.EndUtc.Value) : "—",
                    entry.DurationSeconds.HasValue ? AuditReportFormat.Clock(entry.DurationSeconds.Value) : "—",
                    entry.Kind.ToString(),
                    entry.Name,
                    string.IsNullOrEmpty(entry.Detail) ? "—" : entry.Detail,
                    entry.InteractionId ?? "—",
                    entry.ActorType.ToString(),
                    AuditReportFormat.Timestamp(entry.RecordedUtc),
                ]);

                return entry.Flagged
                    ? row.WithCellStyle(6, ReportStyle.Create(color: FlagColor, bold: true))
                    : row;
            })
            .ToList();

        var section = ReportSection.ForTable(S["Agent activity timeline"].Value, columns, rows);

        if (entries.Count > EntryLimit)
        {
            section.Description = S["The first {0} of {1} entries are listed. Choose an agent or a shorter period to see the rest.", EntryLimit, entries.Count].Value;
        }

        return document.Add(section);
    }
}
