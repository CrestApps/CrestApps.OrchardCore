using CrestApps.OrchardCore.Downloads.Indexes;
using CrestApps.OrchardCore.Downloads.Models;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Downloads.Reports;

/// <summary>
/// The download links given in the period, by product: how many buyers downloaded, how many never did, and how many
/// links ran out, expired or were taken back.
/// </summary>
public sealed class DownloadsReport : IReport
{
    private const int PageSize = 1000;

    private static readonly DateTime _earliest = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly ISession _session;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public DownloadsReport(ISession session, IClock clock, IStringLocalizer<DownloadsReport> stringLocalizer)
    {
        _session = session;
        _clock = clock;
        S = stringLocalizer;
    }

    public string Name => "digital-downloads";

    public LocalizedString DisplayName => S["Downloads"];

    public LocalizedString Description => S["The download links given in the period, by product: how many were used, never used, used up, expired or taken back, and the downloads made with them."];

    public string Category => ReportsConstants.Categories.Commerce;

    public Permission Permission => DownloadsPermissions.ManageDownloads;

    public async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var range = context.Filter.GetDateRange();
        var from = range.FromUtc ?? _earliest;
        var to = range.ToUtc ?? _clock.UtcNow;
        var entitlements = new List<DownloadEntitlement>();

        for (var skip = 0; ; skip += PageSize)
        {
            var page = (await _session
                .Query<DownloadEntitlement, DownloadEntitlementIndex>(index => index.CreatedUtc >= from && index.CreatedUtc < to, collection: DownloadsConstants.CollectionName)
                .OrderBy(index => index.Id)
                .Skip(skip)
                .Take(PageSize)
                .ListAsync(cancellationToken)).ToList();

            entitlements.AddRange(page);

            if (page.Count < PageSize)
            {
                break;
            }
        }

        var totals = DownloadTotals.Summarize(entitlements, _clock.UtcNow);
        var all = DownloadTotals.Combine(totals);
        var document = new ReportDocument { Title = DisplayName.Value };

        document.Add(ReportSection.ForMetrics(S["Summary"].Value,
        [
            new ReportMetric(S["Links given"].Value, ReportFormat.Number(all.Links), S["One per file of each digital product paid for."].Value),
            new ReportMetric(S["Downloaded"].Value, ReportFormat.Number(all.Used), S["Links used at least once."].Value),
            new ReportMetric(S["Never downloaded"].Value, ReportFormat.Number(all.NeverUsed)),
            new ReportMetric(S["Downloads made"].Value, ReportFormat.Number(all.Downloads)),
            new ReportMetric(S["Used up"].Value, ReportFormat.Number(all.UsedUp)),
            new ReportMetric(S["Expired"].Value, ReportFormat.Number(all.Expired)),
            new ReportMetric(S["Taken back"].Value, ReportFormat.Number(all.Revoked), S["Canceled or refunded orders, or taken back from the order's page."].Value),
        ]));

        if (totals.Count == 0)
        {
            return document;
        }

        document.Add(ReportSection.ForTable(S["Products"].Value,
        [
            new ReportColumn(S["Product"].Value),
            new ReportColumn(S["File"].Value),
            new ReportColumn(S["Links given"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Downloaded"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Never downloaded"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Downloads made"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Used up"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Expired"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Taken back"].Value, ReportColumnAlign.End),
        ], [.. totals.Select(total => new ReportRow(
        [
            total.ProductTitle ?? string.Empty,
            total.FileName ?? string.Empty,
            ReportFormat.Number(total.Links),
            ReportFormat.Number(total.Used),
            ReportFormat.Number(total.NeverUsed),
            ReportFormat.Number(total.Downloads),
            ReportFormat.Number(total.UsedUp),
            ReportFormat.Number(total.Expired),
            ReportFormat.Number(total.Revoked),
        ]))]));

        return document;
    }
}

/// <summary>
/// The download links of one product's file, and what became of them.
/// </summary>
public sealed record DownloadTotals(string ProductTitle, string FileName, int Links, int Used, int NeverUsed, int Downloads, int UsedUp, int Expired, int Revoked)
{
    /// <summary>
    /// Sums the links per product and file, the most given first. A link taken back counts as taken back, not as
    /// expired or used up.
    /// </summary>
    public static IReadOnlyList<DownloadTotals> Summarize(IEnumerable<DownloadEntitlement> entitlements, DateTime nowUtc)
        => [.. entitlements
            .GroupBy(entitlement => (entitlement.ProductId ?? string.Empty, entitlement.FileId ?? string.Empty))
            .Select(group =>
            {
                var latest = group.OrderByDescending(entitlement => entitlement.CreatedUtc).First();
                var live = group.Where(entitlement => entitlement.RevokedUtc is null).ToList();

                return new DownloadTotals(
                    latest.ProductTitle,
                    latest.FileName,
                    group.Count(),
                    group.Count(entitlement => entitlement.DownloadCount > 0),
                    group.Count(entitlement => entitlement.DownloadCount == 0),
                    group.Sum(entitlement => entitlement.DownloadCount),
                    live.Count(entitlement => entitlement.DownloadLimit is > 0 && entitlement.DownloadCount >= entitlement.DownloadLimit),
                    live.Count(entitlement => entitlement.ExpiresUtc is { } expires && expires <= nowUtc && !(entitlement.DownloadLimit is > 0 && entitlement.DownloadCount >= entitlement.DownloadLimit)),
                    group.Count(entitlement => entitlement.RevokedUtc is not null));
            })
            .OrderByDescending(total => total.Links)
            .ThenBy(total => total.ProductTitle, StringComparer.OrdinalIgnoreCase)];

    public static DownloadTotals Combine(IEnumerable<DownloadTotals> totals)
        => totals.Aggregate(
            new DownloadTotals(null, null, 0, 0, 0, 0, 0, 0, 0),
            (sum, total) => new DownloadTotals(null, null, sum.Links + total.Links, sum.Used + total.Used, sum.NeverUsed + total.NeverUsed, sum.Downloads + total.Downloads, sum.UsedUp + total.UsedUp, sum.Expired + total.Expired, sum.Revoked + total.Revoked));
}
