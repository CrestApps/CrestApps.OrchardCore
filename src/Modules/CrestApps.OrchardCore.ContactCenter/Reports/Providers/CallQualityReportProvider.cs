using System.Globalization;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Providers;

/// <summary>
/// The Contact Center call quality report: how calls rated, which agents and network paths the poor ones cluster on,
/// what most likely caused them, and each poor call with the figures that made it so.
/// </summary>
/// <remarks>
/// The agent's side of a call is what their soft phone measured, falling back to the provider's measurement of their
/// leg; the customer's side is the provider's measurement of the customer's leg. The two are counted apart, because a
/// customer on a poor line says nothing about the agent, and an agent on a poor network says nothing about the
/// customer.
/// </remarks>
public sealed class CallQualityReportProvider : ContactCenterReportBase
{
    /// <summary>
    /// The most poor calls listed one by one.
    /// </summary>
    internal const int PoorCallListLimit = 200;

    private readonly ICallQualityRecordStore _recordStore;
    private readonly IAgentProfileStore _agentProfileStore;
    private readonly UserManager<IUser> _userManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="CallQualityReportProvider"/> class.
    /// </summary>
    /// <param name="reportingService">The Contact Center reporting service.</param>
    /// <param name="capabilityGuard">The guard that decides whether the producing capabilities are enabled.</param>
    /// <param name="recordStore">The call quality records.</param>
    /// <param name="agentProfileStore">The agent directory, for agent names.</param>
    /// <param name="userManager">The user manager, for the name of an agent whose profile does not carry one.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CallQualityReportProvider(
        IContactCenterReportingService reportingService,
        IContactCenterReportCapabilityGuard capabilityGuard,
        ICallQualityRecordStore recordStore,
        IAgentProfileStore agentProfileStore,
        UserManager<IUser> userManager,
        IStringLocalizer<CallQualityReportProvider> stringLocalizer)
        : base(reportingService, capabilityGuard, stringLocalizer)
    {
        _recordStore = recordStore;
        _agentProfileStore = agentProfileStore;
        _userManager = userManager;
    }

    /// <inheritdoc/>
    public override string Name => "contact-center-call-quality";

    /// <inheritdoc/>
    public override LocalizedString DisplayName => S["Call quality"];

    /// <inheritdoc/>
    public override LocalizedString Description => S["How calls rated, the agents and network paths poor calls cluster on, their likely cause, and each poor call's measurements."];

    /// <inheritdoc/>
    public override IReadOnlyCollection<string> FilterNames { get; } =
    [
        ContactCenterReportFilter.QueueId,
        ContactCenterReportFilter.AgentId,
    ];

    /// <inheritdoc/>
    /// <remarks>Quality is recorded only for voice calls, which only the Voice capability carries.</remarks>
    public override IReadOnlyCollection<string> RequiredFeatureIds { get; } = [
        ContactCenterConstants.Feature.Voice,
    ];

    /// <inheritdoc/>
    protected override async Task<ReportDocument> RunCoreAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var range = context.Filter.GetDateRange();
        var criteria = ContactCenterReportFilter.GetCriteria(context.Filter);

        var records = (await _recordStore.GetObservedBetweenAsync(
            range.FromUtc.GetValueOrDefault(),
            range.ToUtc.GetValueOrDefault(DateTime.MaxValue),
            cancellationToken))
            .Where(record => string.IsNullOrEmpty(criteria.QueueId) || string.Equals(record.QueueId, criteria.QueueId, StringComparison.OrdinalIgnoreCase))
            .Where(record => string.IsNullOrEmpty(criteria.AgentId) || string.Equals(record.AgentId, criteria.AgentId, StringComparison.Ordinal))
            .ToArray();

        var agentCalls = SelectAgentSide(records);
        var customerLegs = records
            .Where(record => record.Source == CallQualitySource.Provider && record.LegRole == CallPartyRole.Customer)
            .ToArray();

        var agentIds = agentCalls
            .Select(record => record.AgentId)
            .Where(agentId => !string.IsNullOrEmpty(agentId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var userNames = await ResolveUserNamesAsync(agentIds, cancellationToken);

        string AgentName(string agentId)
            => ReportValue.UserDisplayName(
                agentId is not null && userNames.TryGetValue(agentId, out var userName) ? userName : null,
                S["(Unknown agent)"].Value);

        var document = new ReportDocument();
        var poorCalls = agentCalls.Where(record => record.Rating == CallQualityRating.Poor).ToArray();
        var poorCustomerLegs = customerLegs.Where(record => record.Rating == CallQualityRating.Poor).ToArray();

        document.Add(ReportSection.ForMetrics(S["Summary"].Value,
        [
            new ReportMetric(S["Calls measured"].Value, ReportFormat.Number(agentCalls.Count)),
            new ReportMetric(S["Good"].Value, ReportFormat.Number(Count(agentCalls, CallQualityRating.Good)), ReportFormat.Percent(Rate(Count(agentCalls, CallQualityRating.Good), agentCalls.Count))),
            new ReportMetric(S["Degraded"].Value, ReportFormat.Number(Count(agentCalls, CallQualityRating.Degraded)), ReportFormat.Percent(Rate(Count(agentCalls, CallQualityRating.Degraded), agentCalls.Count))),
            new ReportMetric(S["Poor"].Value, ReportFormat.Number(poorCalls.Length), ReportFormat.Percent(Rate(poorCalls.Length, agentCalls.Count))),
            new ReportMetric(S["Avg MOS"].Value, FormatNumber(Average(agentCalls, record => record.Mos), "0.00")),
            new ReportMetric(S["Customer legs measured"].Value, ReportFormat.Number(customerLegs.Length)),
            new ReportMetric(S["Poor on the customer's side"].Value, ReportFormat.Number(poorCustomerLegs.Length), ReportFormat.Percent(Rate(poorCustomerLegs.Length, customerLegs.Length))),
        ]));

        var causes = poorCalls
            .Concat(poorCustomerLegs)
            .GroupBy(CallQualityCauseClassifier.Classify)
            .Select(group => (Cause: group.Key, Count: group.Count()))
            .OrderByDescending(entry => entry.Count)
            .ToArray();

        if (causes.Length > 0)
        {
            var max = causes.Max(entry => entry.Count);

            document.Add(ReportSection.ForBars(S["Likely cause of poor calls"].Value,
                causes.Select(entry => new ReportBar(DescribeCause(entry.Cause), ReportFormat.Number(entry.Count), max > 0 ? (double)entry.Count / max : 0))));
        }

        if (agentCalls.Count > 0)
        {
            var columns = new[]
            {
                new ReportColumn(S["Agent"].Value),
                new ReportColumn(S["Calls"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Poor"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Poor %"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Avg MOS"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Worst loss"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Likely cause"].Value),
            };

            var rows = agentCalls
                .GroupBy(record => record.AgentId ?? string.Empty, StringComparer.Ordinal)
                .Select(group => (Group: group, Poor: Count(group, CallQualityRating.Poor)))
                .OrderByDescending(entry => entry.Poor)
                .ThenByDescending(entry => entry.Group.Count())
                .Select(entry => new ReportRow(
                [
                    AgentName(entry.Group.Key),
                    ReportFormat.Number(entry.Group.Count()),
                    ReportFormat.Number(entry.Poor),
                    ReportFormat.Percent(Rate(entry.Poor, entry.Group.Count())),
                    FormatNumber(Average(entry.Group, record => record.Mos), "0.00"),
                    FormatPercent(entry.Group.Max(record => record.LossPercent)),
                    DescribeCause(CallQualityCauseClassifier.ClassifyMostLikely(entry.Group)),
                ]))
                .ToList();

            document.Add(ReportSection.ForTable(S["By agent"].Value, columns, rows));
        }

        var browserCalls = agentCalls.Where(record => record.Browser is not null).ToArray();

        if (browserCalls.Length > 0)
        {
            var columns = new[]
            {
                new ReportColumn(S["Network path"].Value),
                new ReportColumn(S["Calls"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Poor"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Avg MOS"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Avg round trip"].Value, ReportColumnAlign.End),
            };

            var rows = browserCalls
                .GroupBy(record => DescribePath(record.Browser.LocalCandidateType), StringComparer.Ordinal)
                .OrderByDescending(group => group.Count())
                .Select(group => new ReportRow(
                [
                    group.Key,
                    ReportFormat.Number(group.Count()),
                    ReportFormat.Number(Count(group, CallQualityRating.Poor)),
                    FormatNumber(Average(group, record => record.Mos), "0.00"),
                    FormatMilliseconds(Average(group, record => record.RoundTripMs)),
                ]))
                .ToList();

            document.Add(ReportSection.ForTable(S["By network path"].Value, columns, rows));
        }

        var listed = poorCalls
            .Concat(poorCustomerLegs)
            .OrderByDescending(record => record.ObservedUtc)
            .Take(PoorCallListLimit)
            .ToArray();

        if (listed.Length > 0)
        {
            var columns = new[]
            {
                new ReportColumn(S["Ended (UTC)"].Value),
                new ReportColumn(S["Agent"].Value),
                new ReportColumn(S["Side"].Value),
                new ReportColumn(S["MOS"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Loss"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Jitter"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Round trip"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Likely cause"].Value),
                new ReportColumn(S["Microphone"].Value),
                new ReportColumn(S["Network path"].Value),
                new ReportColumn(S["Interaction"].Value),
            };

            var rows = listed
                .Select(record => new ReportRow(
                [
                    record.ObservedUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    AgentName(record.AgentId),
                    record.LegRole == CallPartyRole.Customer ? S["Customer"].Value : S["Agent"].Value,
                    FormatNumber(record.Mos, "0.00"),
                    FormatPercent(record.LossPercent),
                    FormatMilliseconds(record.JitterMs),
                    FormatMilliseconds(record.RoundTripMs),
                    DescribeCause(CallQualityCauseClassifier.Classify(record)),
                    record.Browser?.CaptureDevice ?? string.Empty,
                    record.Browser is null ? string.Empty : DescribePath(record.Browser.LocalCandidateType),
                    record.InteractionId ?? string.Empty,
                ]))
                .ToList();

            var title = listed.Length == PoorCallListLimit
                ? S["Poor calls (the most recent {0})", PoorCallListLimit].Value
                : S["Poor calls"].Value;

            document.Add(ReportSection.ForTable(title, columns, rows));
        }

        return document;
    }

    // An agent profile created before it carried the user name knows only the user's id, so the name is read from
    // the account instead of reporting a real agent as unknown.
    private async Task<Dictionary<string, string>> ResolveUserNamesAsync(string[] agentIds, CancellationToken cancellationToken)
    {
        var userNames = new Dictionary<string, string>(StringComparer.Ordinal);

        if (agentIds.Length == 0)
        {
            return userNames;
        }

        foreach (var agent in await _agentProfileStore.GetAsync(agentIds, cancellationToken))
        {
            var userName = agent.UserName;

            if (string.IsNullOrEmpty(userName) && !string.IsNullOrEmpty(agent.UserId))
            {
                userName = (await _userManager.FindByIdAsync(agent.UserId))?.UserName;
            }

            userNames[agent.ItemId] = userName;
        }

        return userNames;
    }

    /// <summary>
    /// Picks one record per call for the agent's side: the soft phone's when it measured the call, otherwise the
    /// provider's measurement of the agent's leg.
    /// </summary>
    /// <param name="records">Every record in the period.</param>
    /// <returns>One record per agent-side call.</returns>
    internal static IReadOnlyList<CallQualityRecord> SelectAgentSide(IEnumerable<CallQualityRecord> records)
        => records
            .Where(record => record.Source == CallQualitySource.Browser || record.LegRole == CallPartyRole.Agent)
            .OrderBy(record => record.Source == CallQualitySource.Browser ? 0 : 1)
            .DistinctBy(record => record.ProviderCallControlId, StringComparer.Ordinal)
            .ToArray();

    private string DescribeCause(CallQualityCause cause)
        => cause switch
        {
            CallQualityCause.NoAudioReceived => S["No audio received"].Value,
            CallQualityCause.PacketLoss => S["Packet loss"].Value,
            CallQualityCause.Jitter => S["Jitter"].Value,
            CallQualityCause.Latency => S["Latency"].Value,
            CallQualityCause.CustomerSide => S["Customer's line"].Value,
            _ => S["Not determined"].Value,
        };

    // The kind of ICE candidate the soft phone's side of the media flowed through.
    private string DescribePath(string candidateType)
        => candidateType switch
        {
            "host" => S["Direct (local network)"].Value,
            "srflx" or "prflx" => S["Direct (through NAT)"].Value,
            "relay" => S["Relayed (TURN)"].Value,
            _ => S["Unknown"].Value,
        };

    private static int Count(IEnumerable<CallQualityRecord> records, CallQualityRating rating)
        => records.Count(record => record.Rating == rating);

    private static double Rate(int count, int total)
        => total > 0 ? (double)count / total : 0;

    private static double? Average(IEnumerable<CallQualityRecord> records, Func<CallQualityRecord, double?> selector)
    {
        var values = records.Select(selector).Where(value => value.HasValue).Select(value => value.Value).ToArray();

        return values.Length > 0 ? values.Average() : null;
    }

    private static string FormatNumber(double? value, string format)
        => value?.ToString(format, CultureInfo.InvariantCulture) ?? string.Empty;

    private static string FormatPercent(double? value)
        => value.HasValue ? string.Create(CultureInfo.InvariantCulture, $"{value.Value:0.0}%") : string.Empty;

    private static string FormatMilliseconds(double? value)
        => value.HasValue ? string.Create(CultureInfo.InvariantCulture, $"{value.Value:0} ms") : string.Empty;
}
