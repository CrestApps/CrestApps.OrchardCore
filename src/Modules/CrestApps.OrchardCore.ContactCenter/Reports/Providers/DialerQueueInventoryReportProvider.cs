using CrestApps.OrchardCore.ContactCenter.Core;
using OrchardCore.Localization;
using System.Globalization;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models.Dispatch;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Reports.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Providers;

/// <summary>
/// The dialer queue inventory report: what each campaign queue holds right now, grouped by the dialer profile its items
/// are stamped with, which of its records can be dialed now and why the rest are held back.
/// </summary>
/// <remarks>
/// It describes the present, whatever period is chosen: a queue holds what it holds now. The records are classified by
/// <see cref="IDialerQueueInventoryService"/>, which the campaign summary uses too, so the two reports agree.
/// </remarks>
public sealed class DialerQueueInventoryReportProvider : ContactCenterReportBase, IReport
{
    private const string FlagColor = "#B42318";

    private readonly IDialerQueueInventoryService _inventoryService;
    private readonly ICatalogManager<OmnichannelCampaign> _campaignManager;
    private readonly ActivitySourceOptions _activitySourceOptions;
    private readonly ILocalClock _localClock;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialerQueueInventoryReportProvider"/> class.
    /// </summary>
    /// <param name="reportingService">The Contact Center reporting service.</param>
    /// <param name="capabilityGuard">The guard that decides whether the producing capabilities are enabled.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    /// <param name="inventoryService">The service that reads what the dialer holds.</param>
    /// <param name="campaignManager">The campaigns, read for their names and groups.</param>
    /// <param name="activitySourceOptions">The activity sources, read for their display names.</param>
    /// <param name="localClock">The tenant's clock, whose time zone the report's times are in.</param>
    public DialerQueueInventoryReportProvider(
        IContactCenterReportingService reportingService,
        IContactCenterReportCapabilityGuard capabilityGuard,
        IStringLocalizer<DialerQueueInventoryReportProvider> stringLocalizer,
        IDialerQueueInventoryService inventoryService,
        ICatalogManager<OmnichannelCampaign> campaignManager,
        IOptions<ActivitySourceOptions> activitySourceOptions,
        ILocalClock localClock)
        : base(reportingService, capabilityGuard, stringLocalizer)
    {
        _inventoryService = inventoryService;
        _campaignManager = campaignManager;
        _activitySourceOptions = activitySourceOptions.Value;
        _localClock = localClock;
    }

    /// <inheritdoc/>
    public static string Name => "contact-center-dialer-queue-inventory";

    /// <inheritdoc/>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<DialerQueueInventoryReportProvider>("Dialer queue inventory");

    /// <inheritdoc/>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<DialerQueueInventoryReportProvider>("What each campaign queue holds right now, by the dialer profile its items are stamped with, and why the records that are not being dialed are held back.");

    /// <inheritdoc/>
    public static string Category => ReportsConstants.Categories.CrmCampaigns;

    /// <inheritdoc/>
    public static Permission Permission => ContactCenterPermissions.ViewReports;

    /// <inheritdoc/>
    public override IReadOnlyCollection<string> FilterNames { get; } =
    [
        ContactCenterReportFilter.CampaignGroupId,
        ContactCenterReportFilter.CampaignId,
    ];

    /// <inheritdoc/>
    /// <remarks>Campaign queues and their items belong to the Work Distribution feature the reporting feature already depends on.</remarks>
    public override IReadOnlyCollection<string> RequiredFeatureIds { get; } = [];

    /// <inheritdoc/>
    protected override async Task<ReportDocument> RunCoreAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var criteria = ContactCenterReportFilter.GetCriteria(context.Filter);
        var campaigns = (await _campaignManager.GetAllAsync(cancellationToken))
            .Where(campaign => campaign is not null && !string.IsNullOrEmpty(campaign.ItemId))
            .ToDictionary(campaign => campaign.ItemId, StringComparer.Ordinal);

        var inventory = await _inventoryService.GetInventoryAsync(
            new DialerQueueInventoryRequest
            {
                CampaignIds = ResolveCampaignIds(criteria.CampaignId, criteria.CampaignGroupId, campaigns.Values),
            },
            cancellationToken);

        var timeZone = await ReportTimeZone.ResolveAsync(_localClock);
        var document = new ReportDocument();

        var summary = ReportSection.ForMetrics(S["Right now"].Value,
        [
            new ReportMetric(S["Campaign queues"].Value, ReportFormat.Number(inventory.Rows.Where(row => row.QueueId is not null).Select(row => row.QueueId).Distinct(StringComparer.Ordinal).Count())),
            new ReportMetric(S["Waiting"].Value, ReportFormat.Number(inventory.Rows.Sum(row => row.WaitingCount))),
            new ReportMetric(S["Reserved"].Value, ReportFormat.Number(inventory.Rows.Sum(row => row.ReservedCount))),
            new ReportMetric(S["Assigned"].Value, ReportFormat.Number(inventory.Rows.Sum(row => row.AssignedCount))),
            new ReportMetric(S["Pending"].Value, ReportFormat.Number(inventory.Campaigns.Sum(campaign => campaign.PendingCount))),
            new ReportMetric(S["Dialable now"].Value, ReportFormat.Number(inventory.Campaigns.Sum(campaign => campaign.DialableNowCount))),
            new ReportMetric(S["Held back"].Value, ReportFormat.Number(inventory.Campaigns.Sum(campaign => campaign.HeldBackCount))),
            new ReportMetric(S["Not in any queue"].Value, ReportFormat.Number(inventory.Campaigns.Sum(campaign => campaign.NotInAnyQueueCount))),
            new ReportMetric(S["Not explained"].Value, ReportFormat.Number(inventory.Campaigns.Sum(campaign => campaign.NotEvaluatedCount))),
        ]);

        summary.Description = S["Read live when the report runs; the date range does not apply. Times are in {0}.", timeZone.Name].Value;
        document.Add(summary);

        if (inventory.Rows.Count == 0)
        {
            return document;
        }

        var rows = new List<ReportRow>();
        var reasonRows = new List<ReportRow>();

        foreach (var row in inventory.Rows)
        {
            var campaignName = CampaignName(row.CampaignId, campaigns);
            var flags = Flags(row);
            var reportRow = new ReportRow(
            [
                campaignName,
                row.QueueId is null ? S["Not in the campaign queue"].Value : S["Campaign queue"].Value,
                StampedProfile(row),
                row.StampedProfileMode ?? "—",
                row.StampedProfileEnabled switch
                {
                    true => S["Yes"].Value,
                    false => S["No"].Value,
                    _ => "—",
                },
                ActivityProfiles(row.ActivityProfiles),
                ReportFormat.Number(row.WaitingCount),
                ReportFormat.Number(row.ReservedCount),
                ReportFormat.Number(row.AssignedCount),
                FormatTime(row.OldestWaitingUtc, timeZone),
                ReportFormat.Number(row.PendingCount),
                ActivityMix(row.ActivityMix),
                ReportFormat.Number(row.DialableNowCount),
                ReportFormat.Number(row.HeldBackCount),
                HeldBackBecause(row),
                row.Pacing is { } pacing ? DispatchPacingLabels.Describe(S, pacing) : "—",
                flags.Count == 0 ? "—" : string.Join("; ", flags),
                FormatTime(row.LastDialedUtc, timeZone),
            ]);

            if (flags.Count > 0)
            {
                reportRow.WithCellStyle(16, ReportStyle.Create(color: FlagColor, bold: true));
            }

            if (row.StampedProfileEnabled == false || row.StampedProfileMissing)
            {
                reportRow.WithCellStyle(2, ReportStyle.Create(color: FlagColor, bold: true));
            }

            rows.Add(reportRow);

            foreach (var bucket in row.HeldBackReasons)
            {
                reasonRows.Add(new ReportRow(
                [
                    campaignName,
                    StampedProfile(row),
                    DispatchBlockerLabels.Describe(S, bucket.Blocker),
                    ReportFormat.Number(bucket.Count),
                    FormatTime(bucket.NextDueUtc, timeZone),
                ]));
            }
        }

        document.Add(ReportSection.ForTable(
            S["Campaign queues by stamped dialer profile"].Value,
            [
                new ReportColumn(S["Campaign"].Value),
                new ReportColumn(S["Queue"].Value),
                new ReportColumn(S["Stamped dialer profile"].Value),
                new ReportColumn(S["Mode"].Value),
                new ReportColumn(S["Profile on"].Value),
                new ReportColumn(S["Activity dialer profile"].Value),
                new ReportColumn(S["Waiting"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Reserved"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Assigned"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Oldest waiting"].Value),
                new ReportColumn(S["Pending"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Source / type / channel"].Value),
                new ReportColumn(S["Dialable now"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Held back"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Held back because"].Value),
                new ReportColumn(S["Queue pacing"].Value),
                new ReportColumn(S["Flags"].Value),
                new ReportColumn(S["Last dialed"].Value),
            ],
            rows));

        if (reasonRows.Count > 0)
        {
            document.Add(ReportSection.ForTable(
                S["Why records are held back"].Value,
                [
                    new ReportColumn(S["Campaign"].Value),
                    new ReportColumn(S["Stamped dialer profile"].Value),
                    new ReportColumn(S["Reason"].Value),
                    new ReportColumn(S["Count"].Value, ReportColumnAlign.End),
                    new ReportColumn(S["Next due"].Value),
                ],
                reasonRows));
        }

        return document;
    }

    /// <summary>
    /// Resolves the campaigns a campaign or campaign group filter narrows the report to.
    /// </summary>
    /// <param name="campaignId">The selected campaign, if any.</param>
    /// <param name="campaignGroupId">The selected campaign group, if any.</param>
    /// <param name="campaigns">Every campaign.</param>
    /// <returns>The campaigns to cover, or <see langword="null"/> for every campaign.</returns>
    internal static IReadOnlyCollection<string> ResolveCampaignIds(string campaignId, string campaignGroupId, IEnumerable<OmnichannelCampaign> campaigns)
    {
        if (string.IsNullOrEmpty(campaignId) && string.IsNullOrEmpty(campaignGroupId))
        {
            return null;
        }

        if (string.IsNullOrEmpty(campaignGroupId))
        {
            return [campaignId];
        }

        return campaigns
            .Where(campaign => string.Equals(campaign.CampaignGroupId, campaignGroupId, StringComparison.Ordinal) &&
                (string.IsNullOrEmpty(campaignId) || string.Equals(campaign.ItemId, campaignId, StringComparison.Ordinal)))
            .Select(campaign => campaign.ItemId)
            .ToArray();
    }

    private string CampaignName(string campaignId, Dictionary<string, OmnichannelCampaign> campaigns)
    {
        return campaigns.TryGetValue(campaignId, out var campaign) && !string.IsNullOrWhiteSpace(campaign.DisplayText)
            ? campaign.DisplayText
            : S["(Unknown campaign)"].Value;
    }

    private string StampedProfile(DialerQueueInventoryRow row)
    {
        if (row.QueueId is null)
        {
            return "—";
        }

        if (row.StampedProfileId is null)
        {
            return S["(None stamped)"].Value;
        }

        if (row.StampedProfileIsCallback)
        {
            return S["Queued callback"].Value;
        }

        if (row.StampedProfileMissing)
        {
            return S["(No longer exists)"].Value;
        }

        return row.StampedProfileName ?? row.StampedProfileId;
    }

    private string ActivityProfiles(IEnumerable<DialerInventoryProfileCount> profiles)
    {
        var parts = profiles
            .Select(profile => string.Format(
                CultureInfo.CurrentCulture,
                "{0} ({1})",
                profile.ProfileId is null
                    ? S["(None)"].Value
                    : profile.ProfileName ?? S["(No longer exists)"].Value,
                ReportFormat.Number(profile.Count)))
            .ToArray();

        return parts.Length == 0 ? "—" : string.Join("; ", parts);
    }

    private string ActivityMix(IEnumerable<DispatchActivityGroup> mix)
    {
        var parts = mix
            .Select(group => string.Format(
                CultureInfo.CurrentCulture,
                "{0} / {1} / {2} ({3})",
                SourceName(group.Source),
                string.IsNullOrEmpty(group.InteractionType) ? "—" : group.InteractionType,
                string.IsNullOrEmpty(group.Channel) ? "—" : group.Channel,
                ReportFormat.Number(group.Count)))
            .ToArray();

        return parts.Length == 0 ? "—" : string.Join("; ", parts);
    }

    private string SourceName(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return "—";
        }

        return _activitySourceOptions.Sources.TryGetValue(source, out var entry) && entry.DisplayName is not null
            ? entry.DisplayName.Value
            : source;
    }

    private string HeldBackBecause(DialerQueueInventoryRow row)
    {
        var parts = row.HeldBackReasons
            .Select(bucket => string.Format(
                CultureInfo.CurrentCulture,
                "{0} ({1})",
                DispatchBlockerLabels.Describe(S, bucket.Blocker),
                ReportFormat.Number(bucket.Count)))
            .ToList();

        if (row.NotEvaluatedCount > 0)
        {
            parts.Add(string.Format(
                CultureInfo.CurrentCulture,
                "{0} ({1})",
                S["Not explained (more records than one pass reads)"].Value,
                ReportFormat.Number(row.NotEvaluatedCount)));
        }

        return parts.Count == 0 ? "—" : string.Join("; ", parts);
    }

    private List<string> Flags(DialerQueueInventoryRow row)
    {
        var flags = new List<string>();

        if (row.StampedProfileMissing)
        {
            flags.Add(S["The stamped dialer profile no longer exists"].Value);
        }

        if (row.StampedProfileEnabled == false)
        {
            flags.Add(S["The stamped dialer profile is turned off"].Value);
        }

        if (row.ProfileMismatchCount > 0)
        {
            flags.Add(S["{0} stamped differently from their activity", ReportFormat.Number(row.ProfileMismatchCount)].Value);
        }

        if (row.NotInAnyQueueCount > 0)
        {
            flags.Add(S["{0} pending activities in no queue", ReportFormat.Number(row.NotInAnyQueueCount)].Value);
        }

        return flags;
    }

    private static string FormatTime(DateTime? utc, ReportTimeZone timeZone)
    {
        return utc is { } value
            ? timeZone.ToLocal(value).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
            : "—";
    }
}
