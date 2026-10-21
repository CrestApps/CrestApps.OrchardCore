using System.Globalization;
using CrestApps.OrchardCore.Inventory.Core;
using CrestApps.OrchardCore.Inventory.Core.Indexes;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Inventory.Reports;

/// <summary>
/// How stock moved in the period: what each item received, sold, got back and was corrected by, with what is on hand
/// now and how long it lasts at the period's rate of sale.
/// </summary>
public sealed class StockMovementsReport : IReport
{
    private const int PageSize = 1000;

    private static readonly DateTime _earliest = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly ISession _session;
    private readonly IStockItemStore _items;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public StockMovementsReport(ISession session, IStockItemStore items, IClock clock, IStringLocalizer<StockMovementsReport> stringLocalizer)
    {
        _session = session;
        _items = items;
        _clock = clock;
        S = stringLocalizer;
    }

    public string Name => "stock-movements";

    public LocalizedString DisplayName => S["Stock movements"];

    public LocalizedString Description => S["What each item received, sold, got back and was corrected by in the period, with what is on hand now and how many days it lasts at the period's rate of sale."];

    public string Category => ReportsConstants.Categories.Commerce;

    public Permission Permission => InventoryPermissions.ViewInventory;

    public async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var range = context.Filter.GetDateRange();
        var from = range.FromUtc ?? _earliest;
        var to = range.ToUtc ?? _clock.UtcNow;
        var movements = new List<StockMovement>();

        for (var skip = 0; ; skip += PageSize)
        {
            var page = (await _session
                .Query<StockMovement, StockMovementIndex>(index => index.Utc >= from && index.Utc < to, collection: InventoryConstants.CollectionName)
                .OrderBy(index => index.Id)
                .Skip(skip)
                .Take(PageSize)
                .ListAsync(cancellationToken)).ToList();

            movements.AddRange(page);

            if (page.Count < PageSize)
            {
                break;
            }
        }

        var items = new Dictionary<string, StockItem>(StringComparer.Ordinal);

        foreach (var itemId in movements.Select(movement => movement.StockItemId).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal))
        {
            if (await _items.FindByIdAsync(itemId, cancellationToken) is { } item)
            {
                items[itemId] = item;
            }
        }

        // The period's length, up to now, sets the rate of sale; a period that ended earlier still uses its own length.
        var days = Math.Max(1d, ((to < _clock.UtcNow ? to : _clock.UtcNow) - (from == _earliest && movements.Count > 0 ? movements.Min(movement => movement.Utc) : from)).TotalDays);
        var totals = StockMovementTotals.ByItem(movements, itemId => items.GetValueOrDefault(itemId), days);
        var document = new ReportDocument { Title = DisplayName.Value };

        document.Add(ReportSection.ForMetrics(S["Summary"].Value,
        [
            new ReportMetric(S["Items that moved"].Value, ReportFormat.Number(totals.Count)),
            new ReportMetric(S["Units received"].Value, ReportFormat.Number(totals.Sum(total => (long)total.Received))),
            new ReportMetric(S["Units sold"].Value, ReportFormat.Number(totals.Sum(total => (long)total.Sold))),
            new ReportMetric(S["Units returned"].Value, ReportFormat.Number(totals.Sum(total => (long)total.Returned))),
            new ReportMetric(S["Corrections"].Value, Signed(totals.Sum(total => total.Corrected)), S["Adjustments and counts, net."].Value),
            new ReportMetric(S["Running out within 14 days"].Value, ReportFormat.Number(totals.Count(total => total.DaysLeft is <= 14)), S["At the period's rate of sale."].Value),
        ]));

        if (totals.Count == 0)
        {
            return document;
        }

        var topSellers = totals.Where(total => total.Sold > 0).OrderByDescending(total => total.Sold).Take(10).ToArray();

        if (topSellers.Length > 0)
        {
            document.Add(ReportSection.ForBars(S["Most sold"].Value,
                [.. topSellers.Select(total => new ReportBar(total.Title, ReportFormat.Number(total.Sold), (double)total.Sold / topSellers[0].Sold))]));
        }

        document.Add(ReportSection.ForTable(S["Items"].Value,
        [
            new ReportColumn(S["Item"].Value),
            new ReportColumn(S["SKU"].Value),
            new ReportColumn(S["Received"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Sold"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Returned"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Corrections"].Value, ReportColumnAlign.End),
            new ReportColumn(S["On hand now"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Days of stock left"].Value, ReportColumnAlign.End),
        ], [.. totals.Select(total => new ReportRow(
        [
            total.Title,
            total.Sku ?? string.Empty,
            ReportFormat.Number(total.Received),
            ReportFormat.Number(total.Sold),
            ReportFormat.Number(total.Returned),
            Signed(total.Corrected),
            total.OnHand is { } onHand ? ReportFormat.Number(onHand) : S["Removed"].Value,
            total.DaysLeft is { } left ? left.ToString("0", CultureInfo.CurrentCulture) : S["n/a"].Value,
        ]))]));

        return document;
    }

    private static string Signed(int value)
        => value > 0 ? "+" + ReportFormat.Number(value) : ReportFormat.Number(value);
}

/// <summary>
/// How one item's stock moved in a period.
/// </summary>
public sealed record StockMovementTotals(string StockItemId, string Title, string Sku, int Received, int Sold, int Returned, int Corrected, int? OnHand, double? DaysLeft)
{
    /// <summary>
    /// Sums the movements per item, the most sold first. Sold units are counted as a positive number. Days left is what
    /// is on hand over the units sold a day in the period, and is empty when nothing sold.
    /// </summary>
    public static IReadOnlyList<StockMovementTotals> ByItem(IEnumerable<StockMovement> movements, Func<string, StockItem> item, double days)
        => [.. movements
            .GroupBy(movement => movement.StockItemId ?? string.Empty, StringComparer.Ordinal)
            .Select(group =>
            {
                var stock = item(group.Key);
                var sold = -group.Where(movement => movement.Kind == StockMovementKind.Sold).Sum(movement => movement.Quantity);
                var onHand = stock?.OnHand;
                double? left = sold > 0 && onHand is { } units ? Math.Max(0, units) / (sold / Math.Max(1d, days)) : null;

                return new StockMovementTotals(
                    group.Key,
                    stock?.Title ?? group.Key,
                    stock?.Sku,
                    group.Where(movement => movement.Kind == StockMovementKind.Received).Sum(movement => movement.Quantity),
                    Math.Abs(sold),
                    group.Where(movement => movement.Kind == StockMovementKind.Returned).Sum(movement => movement.Quantity),
                    group.Where(movement => movement.Kind is StockMovementKind.Adjusted or StockMovementKind.Counted).Sum(movement => movement.Quantity),
                    onHand,
                    left);
            })
            .OrderByDescending(total => total.Sold)
            .ThenBy(total => total.Title, StringComparer.OrdinalIgnoreCase)];
}
