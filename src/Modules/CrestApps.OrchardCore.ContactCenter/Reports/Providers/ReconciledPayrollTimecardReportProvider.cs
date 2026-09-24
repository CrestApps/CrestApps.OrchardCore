using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Providers;

/// <summary>
/// The payroll timecard that proves itself: per agent per day in the tenant's time zone, the time signed in beside the
/// time in each state, to the second, with a check that says whether they add up.
/// </summary>
/// <remarks>
/// Signed-in time is measured from sign-ins and sign-offs alone, and state time from every state change in between,
/// so the two are independent measurements of the same day. A day reconciles when they agree exactly and no state
/// change is missing from the record; any other day is flagged, and the report counts them.
/// </remarks>
public sealed class ReconciledPayrollTimecardReportProvider : ContactCenterReportBase
{
    private const string FlagColor = "#B42318";

    private readonly IInteractionEventStore _eventStore;
    private readonly IAgentProfileManager _agentManager;
    private readonly IClock _clock;
    private readonly ILocalClock _localClock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReconciledPayrollTimecardReportProvider"/> class.
    /// </summary>
    /// <param name="reportingService">The Contact Center reporting service.</param>
    /// <param name="capabilityGuard">The guard that decides whether the producing capabilities are enabled.</param>
    /// <param name="eventStore">The event log.</param>
    /// <param name="agentManager">The agent directory, for agent names.</param>
    /// <param name="clock">The clock, so an agent still signed in is counted up to now and no further.</param>
    /// <param name="localClock">The tenant's clock, whose time zone the days and times of day are in.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ReconciledPayrollTimecardReportProvider(
        IContactCenterReportingService reportingService,
        IContactCenterReportCapabilityGuard capabilityGuard,
        IInteractionEventStore eventStore,
        IAgentProfileManager agentManager,
        IClock clock,
        ILocalClock localClock,
        IStringLocalizer<ReconciledPayrollTimecardReportProvider> stringLocalizer)
        : base(reportingService, capabilityGuard, stringLocalizer)
    {
        _eventStore = eventStore;
        _agentManager = agentManager;
        _clock = clock;
        _localClock = localClock;
    }

    /// <inheritdoc/>
    public override string Name => "contact-center-payroll-reconciled-timecard";

    /// <inheritdoc/>
    public override LocalizedString DisplayName => S["Reconciled payroll timecard"];

    /// <inheritdoc/>
    public override LocalizedString Description => S["Per agent per day, signed-in time beside the time in each state to the second, with a check that flags every day that does not add up."];

    /// <inheritdoc/>
    public override string Category => ReportsConstants.Categories.WorkforcePayroll;

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
            .ToDictionary(agent => agent.ItemId, StringComparer.Ordinal);
        var filtered = !string.IsNullOrEmpty(criteria.AgentId);

        var events = await AgentStateEventReader.ReadAsync(
            _eventStore,
            filtered ? [criteria.AgentId] : agents.Keys,
            onlyTheseAgents: filtered,
            fromUtc,
            toUtc,
            additionalEventTypes: null,
            cancellationToken);

        // The period was chosen as local dates, so its days are the tenant's days too.
        var timeZone = await ReportTimeZone.ResolveAsync(_localClock);
        var days = ReconciledTimecard.Build(AgentStateTimeline.Build(events), fromUtc, toUtc, timeZone);

        return Build(days, agentId => ResolveAgentName(agentId, agents), timeZone);
    }

    /// <summary>
    /// Renders the timecard.
    /// </summary>
    /// <param name="days">The agent days.</param>
    /// <param name="agentName">Resolves an agent's display name.</param>
    /// <param name="timeZone">The zone the days and times of day are in; UTC when none is given.</param>
    /// <returns>The report document.</returns>
    internal ReportDocument Build(IReadOnlyList<ReconciledTimecardDay> days, Func<string, string> agentName, ReportTimeZone timeZone = null)
    {
        timeZone ??= ReportTimeZone.Utc;

        var unreconciled = days.Count(day => !day.IsReconciled);

        var document = new ReportDocument()
            .Add(ReportSection.ForMetrics(S["Summary"].Value,
            [
                new ReportMetric(S["Agent days"].Value, ReportFormat.Number(days.Count)),
                new ReportMetric(S["Reconciled days"].Value, ReportFormat.Number(days.Count - unreconciled)),
                new ReportMetric(
                    S["Unreconciled days"].Value,
                    ReportFormat.Number(unreconciled),
                    unreconciled > 0 ? S["Check each flagged day in the agent activity timeline."].Value : null),
                new ReportMetric(S["Signed-in time"].Value, AuditReportFormat.Clock(days.Sum(day => day.SignedInSeconds))),
            ]));

        var columns = new[]
        {
            new ReportColumn(S["Date ({0})", timeZone.Name].Value),
            new ReportColumn(S["Agent"].Value),
            new ReportColumn(S["First in"].Value),
            new ReportColumn(S["Last out"].Value),
            new ReportColumn(S["Signed in"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Available"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Reserved"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Busy"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Wrap-up"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Break + away"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Meeting + training"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Other not ready"].Value, ReportColumnAlign.End),
            new ReportColumn(S["States total"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Difference"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Check"].Value),
        };

        var rows = new List<ReportRow>();

        foreach (var day in days)
        {
            var states = day.States;

            // Rounded together, so the state columns add up to the states total exactly as shown.
            var parts = AuditReportFormat.RoundToWholeSeconds(
            [
                states.AvailableSeconds,
                states.ReservedSeconds,
                states.BusySeconds,
                states.WrapUpSeconds,
                states.BreakAndAwaySeconds,
                states.MeetingAndTrainingSeconds,
                states.OtherNotReadySeconds,
            ]);

            var row = new ReportRow(
            [
                day.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                agentName(day.AgentId),
                day.FirstInUtc.HasValue ? AuditReportFormat.TimeOfDay(timeZone.ToLocal(day.FirstInUtc.Value)) : "—",
                day.LastOutUtc.HasValue
                    ? day.EndsSignedOff
                        ? AuditReportFormat.TimeOfDay(timeZone.ToLocal(day.LastOutUtc.Value))
                        : S["Signed in at {0}", AuditReportFormat.TimeOfDay(timeZone.ToLocal(day.LastOutUtc.Value))].Value
                    : "—",
                AuditReportFormat.Clock(day.SignedInSeconds),
                .. parts.Select(AuditReportFormat.Clock),
                AuditReportFormat.Clock(parts.Sum()),
                FormatDifference(day.DifferenceSeconds),
                Describe(day),
            ]);

            if (!day.IsReconciled)
            {
                row.WithCellStyle(columns.Length - 2, ReportStyle.Create(color: FlagColor, bold: true))
                    .WithCellStyle(columns.Length - 1, ReportStyle.Create(color: FlagColor, bold: true));
            }

            rows.Add(row);
        }

        if (days.Count > 0)
        {
            var parts = AuditReportFormat.RoundToWholeSeconds(
            [
                days.Sum(day => day.States.AvailableSeconds),
                days.Sum(day => day.States.ReservedSeconds),
                days.Sum(day => day.States.BusySeconds),
                days.Sum(day => day.States.WrapUpSeconds),
                days.Sum(day => day.States.BreakAndAwaySeconds),
                days.Sum(day => day.States.MeetingAndTrainingSeconds),
                days.Sum(day => day.States.OtherNotReadySeconds),
            ]);

            rows.Add(new ReportRow(
            [
                S["All dates"].Value,
                S["All agents"].Value,
                "—",
                "—",
                AuditReportFormat.Clock(days.Sum(day => day.SignedInSeconds)),
                .. parts.Select(AuditReportFormat.Clock),
                AuditReportFormat.Clock(parts.Sum()),
                FormatDifference(days.Sum(day => day.DifferenceSeconds)),
                unreconciled == 0 ? S["Reconciled"].Value : S["{0} unreconciled days", unreconciled].Value,
            ], ReportRowKind.GrandTotal));
        }

        return document.Add(ReportSection.ForTable(S["Reconciled payroll timecard"].Value, columns, rows));
    }

    private string Describe(ReconciledTimecardDay day)
    {
        var problems = new List<string>();

        if (day.DifferenceSeconds >= ReconciledTimecardDay.Tolerance)
        {
            problems.Add(S["States recorded outside a sign-in for {0}", FormatMagnitude(day.DifferenceSeconds)].Value);
        }
        else if (day.DifferenceSeconds <= -ReconciledTimecardDay.Tolerance)
        {
            problems.Add(S["Signed-in time not accounted for: {0}", FormatMagnitude(-day.DifferenceSeconds)].Value);
        }

        if (day.MissingTransitions > 0)
        {
            problems.Add(S["{0} state changes missing from the record", day.MissingTransitions].Value);
        }

        if (problems.Count == 0)
        {
            return S["Reconciled"].Value;
        }

        if (day.FromLegacy)
        {
            problems.Add(S["includes time from the older presence events, which do not record reservations, calls or wrap-up"].Value);
        }

        return string.Join("; ", problems);
    }

    private static string FormatDifference(double seconds)
        => Math.Abs(seconds) < ReconciledTimecardDay.Tolerance
            ? AuditReportFormat.Clock(0L)
            : (seconds > 0 ? "+" : "-") + FormatMagnitude(Math.Abs(seconds));

    // Below a second the fraction is the whole story, so it is shown rather than rounded away.
    private static string FormatMagnitude(double seconds)
        => seconds < 1
            ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{seconds:0.000}s")
            : AuditReportFormat.Clock(seconds);

    private string ResolveAgentName(string agentId, Dictionary<string, AgentProfile> agents)
        => ReportValue.UserDisplayName(
            agentId is not null && agents.TryGetValue(agentId, out var agent) ? agent.UserName : null,
            S["(Unknown agent)"].Value);
}
