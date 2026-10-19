using System.Globalization;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models.Dispatch;
using CrestApps.OrchardCore.ContactCenter.Core.Models.Reports;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Reports.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Localization;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Providers;

/// <summary>
/// The Contact Center campaign summary report: per-campaign completed-versus-pending activity progress, with each
/// campaign's pending records split by what the dialer can do with them right now.
/// </summary>
/// <remarks>
/// The split is read through <see cref="IDialerQueueInventoryService"/>, the computation behind the dialer queue inventory
/// report, so on the same records the two reports give the same numbers. The period decides which records are counted;
/// where those records stand with the dialer is true of right now, whatever the period.
/// </remarks>
public sealed class CampaignSummaryReportProvider : ContactCenterReportBase, IReport
{
    /// <summary>
    /// How many distinct attempt counts the attempts distribution shows before it groups the rest.
    /// </summary>
    internal const int AttemptBuckets = 5;

    private readonly IDialerQueueInventoryService _inventoryService;
    private readonly ILocalClock _localClock;

    /// <summary>
    /// Initializes a new instance of the <see cref="CampaignSummaryReportProvider"/> class.
    /// </summary>
    /// <param name="reportingService">The Contact Center reporting service.</param>
    /// <param name="capabilityGuard">The guard that decides whether the producing capabilities are enabled.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    /// <param name="inventoryService">The service that classifies pending records the way the dispatcher decides them.</param>
    /// <param name="localClock">The tenant's clock, whose time zone the report's times are in.</param>
    public CampaignSummaryReportProvider(
        IContactCenterReportingService reportingService,
        IContactCenterReportCapabilityGuard capabilityGuard,
        IStringLocalizer<CampaignSummaryReportProvider> stringLocalizer,
        IDialerQueueInventoryService inventoryService,
        ILocalClock localClock)
        : base(reportingService, capabilityGuard, stringLocalizer)
    {
        _inventoryService = inventoryService;
        _localClock = localClock;
    }

    /// <inheritdoc/>
    public static string Name => "contact-center-campaign-summary";

    /// <inheritdoc/>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<CampaignSummaryReportProvider>("Campaign summary");

    /// <inheritdoc/>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<CampaignSummaryReportProvider>("Per-campaign completed-versus-pending progress across the activity inventory, with the pending records split by what the dialer can do with them right now.");

    /// <inheritdoc/>
    public static string Category => ReportsConstants.Categories.CrmCampaigns;

    /// <inheritdoc/>
    public static Permission Permission => ContactCenterPermissions.ViewReports;

    /// <inheritdoc/>
    public override IReadOnlyCollection<string> FilterNames { get; } =
    [
        ContactCenterReportFilter.CampaignGroupId,
        ContactCenterReportFilter.CampaignId,
        ContactCenterReportFilter.Channel,
        ContactCenterReportFilter.ActivitySource,
        ContactCenterReportFilter.ActivityStatus,
        ContactCenterReportFilter.InteractionType,
        ContactCenterReportFilter.DialerProfileId,
        ContactCenterReportFilter.QueueState,
    ];

    /// <inheritdoc/>
    /// <remarks>Campaigns and their activities are CRM data written by the Omnichannel activity feature the reporting feature already depends on.</remarks>
    public override IReadOnlyCollection<string> RequiredFeatureIds { get; } = [];

    /// <inheritdoc/>
    protected override async Task<ReportDocument> RunCoreAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var range = context.Filter.GetDateRange();
        var fromUtc = range.FromUtc.GetValueOrDefault();
        var toUtc = range.ToUtc.GetValueOrDefault();

        var activities = await ReportingService.GetCampaignActivitiesAsync(
            fromUtc,
            toUtc,
            ContactCenterReportFilter.GetCriteria(context.Filter),
            cancellationToken);

        var inventory = await _inventoryService.GetInventoryAsync(
            new DialerQueueInventoryRequest
            {
                PendingActivities = activities,
                IncludeQueueRows = false,
            },
            cancellationToken);

        var records = inventory.Records.ToDictionary(record => record.ActivityId, StringComparer.Ordinal);
        var covered = Narrow(
            activities,
            records,
            ContactCenterReportFilter.GetString(context.Filter, ContactCenterReportFilter.DialerProfileId),
            ContactCenterReportFilter.GetQueueState(context.Filter));

        var report = await ReportingService.BuildCampaignSummaryAsync(fromUtc, toUtc, covered, cancellationToken);
        var coveredIds = covered.Select(activity => activity.ItemId).ToHashSet(StringComparer.Ordinal);
        var splits = _inventoryService
            .Summarize(inventory.Records.Where(record => coveredIds.Contains(record.ActivityId)))
            .ToDictionary(split => split.CampaignId, StringComparer.Ordinal);

        var lastAttempts = await _inventoryService.GetLastAttemptsAsync(coveredIds, cancellationToken);
        var timeZone = await ReportTimeZone.ResolveAsync(_localClock);
        var byCampaign = covered
            .GroupBy(activity => activity.CampaignId ?? string.Empty, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);

        var totalSplit = Total(splits.Values);
        var document = new ReportDocument();

        document.Add(ReportSection.ForMetrics(S["Summary"].Value,
        [
            new ReportMetric(S["Total"].Value, ReportFormat.Number(report.Totals.Total)),
            new ReportMetric(S["Completed"].Value, ReportFormat.Number(report.Totals.Completed), ReportFormat.Percent(report.Totals.CompletionRate)),
            new ReportMetric(S["Pending"].Value, ReportFormat.Number(report.Totals.Pending)),
            new ReportMetric(S["Dialable now"].Value, ReportFormat.Number(totalSplit.DialableNowCount)),
            new ReportMetric(S["Held back"].Value, ReportFormat.Number(totalSplit.OtherHeldBackCount)),
            new ReportMetric(S["Scheduled for later"].Value, ReportFormat.Number(totalSplit.ScheduledForLaterCount)),
            new ReportMetric(S["Not in any queue"].Value, ReportFormat.Number(totalSplit.NotInAnyQueueCount)),
            new ReportMetric(S["Assigned to a user"].Value, ReportFormat.Number(totalSplit.AssignedToUserCount)),
            new ReportMetric(S["In progress"].Value, ReportFormat.Number(report.Totals.InProgress)),
            new ReportMetric(S["Not in service"].Value, ReportFormat.Number(report.Totals.NotInService)),
        ]));

        var noCampaign = S["(No campaign)"].Value;
        var unknownCampaign = S["(Unknown campaign)"].Value;
        var rows = new List<ReportRow>();

        foreach (var row in report.Rows)
        {
            var label = string.IsNullOrEmpty(row.CampaignId)
                ? noCampaign
                : string.IsNullOrEmpty(row.CampaignName)
                    ? unknownCampaign
                    : row.CampaignName;

            rows.Add(CampaignRow(
                label,
                row.Counts,
                string.IsNullOrEmpty(row.CampaignId) ? null : splits.GetValueOrDefault(row.CampaignId) ?? new DialerCampaignDispatchSummary { CampaignId = row.CampaignId },
                byCampaign.GetValueOrDefault(row.CampaignId ?? string.Empty) ?? [],
                lastAttempts,
                timeZone,
                ReportRowKind.Detail));
        }

        rows.Add(CampaignRow(
            S["All campaigns"].Value,
            report.Totals,
            totalSplit,
            covered,
            lastAttempts,
            timeZone,
            ReportRowKind.GrandTotal));

        var section = ReportSection.ForTable(S["Campaigns"].Value, CampaignColumns(), rows);
        section.Description = S["Dialable now, Held back, Scheduled for later, Not in any queue, Assigned to a user and Not explained split Pending. They are where the period's pending records stand with the dialer right now; pending records that belong to no campaign are not split. Times are in {0}.", timeZone.Name].Value;
        document.Add(section);

        AddReasonsSection(document, report, splits, timeZone);

        var noCampaignGroup = S["(No campaign group)"].Value;
        var unknownCampaignGroup = S["(Unknown campaign group)"].Value;
        var groupRows = new List<ReportRow>();

        foreach (var row in report.GroupRows)
        {
            groupRows.Add(ContactCenterReportCells.ProgressRow(
                string.IsNullOrEmpty(row.CampaignGroupId)
                    ? noCampaignGroup
                    : string.IsNullOrEmpty(row.CampaignGroupName)
                        ? unknownCampaignGroup
                        : row.CampaignGroupName,
                row.Counts,
                ReportRowKind.Subtotal));
        }

        groupRows.Add(ContactCenterReportCells.ProgressRow(
            S["All campaign groups"].Value,
            report.Totals,
            ReportRowKind.GrandTotal));
        document.Add(ReportSection.ForTable(
            S["Campaign groups"].Value,
            ContactCenterReportCells.ProgressColumns(S, S["Campaign group"].Value),
            groupRows));

        return document;
    }

    /// <summary>
    /// Narrows the period's activities to a dialer profile and a queue state.
    /// </summary>
    /// <param name="activities">The period's activities, already narrowed by the other filters.</param>
    /// <param name="records">The classified pending records, by activity identifier.</param>
    /// <param name="dialerProfileId">The selected dialer profile, if any. An activity matches when it carries the profile
    /// or its queue item is stamped with it, because the dialer dials under the stamp.</param>
    /// <param name="queueState">The selected queue state, if any. Only pending records in that state match.</param>
    /// <returns>The activities the report counts.</returns>
    internal static IReadOnlyList<OmnichannelActivityIndex> Narrow(
        IReadOnlyList<OmnichannelActivityIndex> activities,
        IReadOnlyDictionary<string, DialerInventoryRecord> records,
        string dialerProfileId,
        DialerQueueState? queueState)
    {
        if (string.IsNullOrEmpty(dialerProfileId) && queueState is null)
        {
            return activities;
        }

        return activities
            .Where(activity =>
            {
                var record = records.GetValueOrDefault(activity.ItemId);

                if (!string.IsNullOrEmpty(dialerProfileId) &&
                    !string.Equals(activity.DialerProfileId, dialerProfileId, StringComparison.Ordinal) &&
                    !string.Equals(record?.StampedProfileId, dialerProfileId, StringComparison.Ordinal))
                {
                    return false;
                }

                return queueState is null || record?.State == queueState;
            })
            .ToArray();
    }

    /// <summary>
    /// Describes how many times a set of records has been attempted.
    /// </summary>
    /// <param name="activities">The records.</param>
    /// <returns>Each attempt count with how many records have it, lowest first, the counts from
    /// <see cref="AttemptBuckets"/> up grouped together.</returns>
    internal static IReadOnlyList<(string Attempts, int Count)> AttemptsDistribution(IEnumerable<OmnichannelActivityIndex> activities)
    {
        return activities
            .GroupBy(activity => Math.Min(Math.Max(0, activity.Attempts), AttemptBuckets))
            .OrderBy(group => group.Key)
            .Select(group => (
                group.Key == AttemptBuckets
                    ? $"{AttemptBuckets.ToString(CultureInfo.InvariantCulture)}+"
                    : group.Key.ToString(CultureInfo.InvariantCulture),
                group.Count()))
            .ToArray();
    }

    private ReportColumn[] CampaignColumns()
    {
        return
        [
            .. ContactCenterReportCells.ProgressColumns(S, S["Campaign"].Value),
            new ReportColumn(S["Dialable now"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Held back"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Held back because"].Value),
            new ReportColumn(S["Scheduled for later"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Not in any queue"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Assigned to a user"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Not explained"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Attempts distribution"].Value),
            new ReportColumn(S["Last attempt"].Value),
            new ReportColumn(S["Next due"].Value),
            new ReportColumn(S["Stamped dialer profile"].Value),
        ];
    }

    private ReportRow CampaignRow(
        string label,
        ActivityProgressCounts counts,
        DialerCampaignDispatchSummary split,
        IReadOnlyCollection<OmnichannelActivityIndex> activities,
        IReadOnlyDictionary<string, DateTime> lastAttempts,
        ReportTimeZone timeZone,
        ReportRowKind kind)
    {
        var cells = ContactCenterReportCells.Progress(label, counts).ToList();

        if (split is null)
        {
            cells.AddRange(Enumerable.Repeat("—", 7));
        }
        else
        {
            cells.Add(ReportFormat.Number(split.DialableNowCount));
            cells.Add(ReportFormat.Number(split.OtherHeldBackCount));
            cells.Add(Join(split.OtherHeldBackReasons.Select(bucket => (DispatchBlockerLabels.Describe(S, bucket.Blocker), bucket.Count))));
            cells.Add(ReportFormat.Number(split.ScheduledForLaterCount));
            cells.Add(ReportFormat.Number(split.NotInAnyQueueCount));
            cells.Add(ReportFormat.Number(split.AssignedToUserCount));
            cells.Add(ReportFormat.Number(split.NotEvaluatedCount));
        }

        DateTime? lastAttemptUtc = null;

        foreach (var activity in activities)
        {
            if (lastAttempts.TryGetValue(activity.ItemId, out var attemptUtc) && (lastAttemptUtc is null || attemptUtc > lastAttemptUtc))
            {
                lastAttemptUtc = attemptUtc;
            }
        }

        cells.Add(Join(AttemptsDistribution(activities)));
        cells.Add(FormatTime(lastAttemptUtc, timeZone));
        cells.Add(split is null ? "—" : FormatTime(split.NextDueUtc, timeZone));
        cells.Add(split is null
            ? "—"
            : Join(split.StampedProfiles.Select(profile => (StampedProfileName(profile), profile.Count))));

        return new ReportRow(cells, kind);
    }

    // The reasons behind each campaign's pending records that are not dialable now, as one table.
    private void AddReasonsSection(
        ReportDocument document,
        CampaignSummaryReport report,
        Dictionary<string, DialerCampaignDispatchSummary> splits,
        ReportTimeZone timeZone)
    {
        var reasonRows = new List<ReportRow>();

        foreach (var row in report.Rows.Where(row => !string.IsNullOrEmpty(row.CampaignId)))
        {
            if (!splits.TryGetValue(row.CampaignId, out var split))
            {
                continue;
            }

            var name = string.IsNullOrEmpty(row.CampaignName) ? S["(Unknown campaign)"].Value : row.CampaignName;

            foreach (var bucket in split.Buckets.Where(bucket => bucket.Blocker != DispatchBlocker.DialableNow))
            {
                reasonRows.Add(new ReportRow(
                [
                    name,
                    DispatchBlockerLabels.Describe(S, bucket.Blocker),
                    ReportFormat.Number(bucket.Count),
                    FormatTime(bucket.NextDueUtc, timeZone),
                ]));
            }

            if (split.NotEvaluatedCount > 0)
            {
                reasonRows.Add(new ReportRow(
                [
                    name,
                    S["Not explained (more records than one pass reads)"].Value,
                    ReportFormat.Number(split.NotEvaluatedCount),
                    "—",
                ]));
            }
        }

        if (reasonRows.Count == 0)
        {
            return;
        }

        document.Add(ReportSection.ForTable(
            S["Why pending records are not dialing"].Value,
            [
                new ReportColumn(S["Campaign"].Value),
                new ReportColumn(S["Reason"].Value),
                new ReportColumn(S["Pending"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Next due"].Value),
            ],
            reasonRows));
    }

    private string StampedProfileName(DialerInventoryProfileCount profile)
    {
        if (profile.ProfileId is null)
        {
            return S["(None stamped)"].Value;
        }

        if (QueueCallbackDialerProfile.IsCallbackProfile(profile.ProfileId))
        {
            return S["Queued callback"].Value;
        }

        return profile.ProfileName ?? S["(No longer exists)"].Value;
    }

    private static string Join(IEnumerable<(string Label, int Count)> parts)
    {
        var text = string.Join(
            "; ",
            parts.Select(part => string.Format(CultureInfo.CurrentCulture, "{0} ({1})", part.Label, ReportFormat.Number(part.Count))));

        return text.Length == 0 ? "—" : text;
    }

    private static DialerCampaignDispatchSummary Total(IEnumerable<DialerCampaignDispatchSummary> splits)
    {
        var total = new DialerCampaignDispatchSummary();
        var buckets = new Dictionary<DispatchBlocker, DispatchBucket>();
        var otherReasons = new Dictionary<DispatchBlocker, DispatchBucket>();
        var stamps = new Dictionary<string, DialerInventoryProfileCount>(StringComparer.Ordinal);

        foreach (var split in splits)
        {
            total.PendingCount += split.PendingCount;
            total.DialableNowCount += split.DialableNowCount;
            total.HeldBackCount += split.HeldBackCount;
            total.ScheduledForLaterCount += split.ScheduledForLaterCount;
            total.NotInAnyQueueCount += split.NotInAnyQueueCount;
            total.AssignedToUserCount += split.AssignedToUserCount;
            total.NotEvaluatedCount += split.NotEvaluatedCount;

            if (split.NextDueUtc is { } due && (total.NextDueUtc is null || due < total.NextDueUtc))
            {
                total.NextDueUtc = due;
            }

            Merge(buckets, split.Buckets);
            Merge(otherReasons, split.OtherHeldBackReasons);

            foreach (var stamp in split.StampedProfiles)
            {
                var key = stamp.ProfileId ?? string.Empty;

                if (!stamps.TryGetValue(key, out var merged))
                {
                    merged = new DialerInventoryProfileCount
                    {
                        ProfileId = stamp.ProfileId,
                        ProfileName = stamp.ProfileName,
                    };

                    stamps[key] = merged;
                }

                merged.Count += stamp.Count;
            }
        }

        total.Buckets = [.. buckets.Values.OrderByDescending(bucket => bucket.Count).ThenBy(bucket => bucket.Blocker)];
        total.OtherHeldBackReasons = [.. otherReasons.Values.OrderByDescending(bucket => bucket.Count).ThenBy(bucket => bucket.Blocker)];
        total.StampedProfiles = [.. stamps.Values.OrderByDescending(stamp => stamp.Count).ThenBy(stamp => stamp.ProfileId, StringComparer.Ordinal)];

        return total;
    }

    private static void Merge(Dictionary<DispatchBlocker, DispatchBucket> merged, IEnumerable<DispatchBucket> buckets)
    {
        foreach (var bucket in buckets)
        {
            if (!merged.TryGetValue(bucket.Blocker, out var total))
            {
                total = new DispatchBucket { Blocker = bucket.Blocker };
                merged[bucket.Blocker] = total;
            }

            total.Count += bucket.Count;

            if (bucket.NextDueUtc is { } due && (total.NextDueUtc is null || due < total.NextDueUtc))
            {
                total.NextDueUtc = due;
            }
        }
    }

    private static string FormatTime(DateTime? utc, ReportTimeZone timeZone)
    {
        return utc is { } value
            ? timeZone.ToLocal(value).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
            : "—";
    }
}
