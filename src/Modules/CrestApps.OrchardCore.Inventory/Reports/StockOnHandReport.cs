using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Inventory.Reports;

/// <summary>
/// What is in stock right now: each counted item with its units on hand, held and available, the ones running out
/// first. It describes the moment it is run, so the period doesn't change it.
/// </summary>
public sealed class StockOnHandReport : IReport
{
    private const int PageSize = 500;

    private readonly IStockItemStore _items;

    internal readonly IStringLocalizer S;

    public StockOnHandReport(IStockItemStore items, IStringLocalizer<StockOnHandReport> stringLocalizer)
    {
        _items = items;
        S = stringLocalizer;
    }

    public static string Name => "stock-on-hand";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<StockOnHandReport>("Stock on hand");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<StockOnHandReport>("Every counted item with its units on hand, held for orders and available, out of stock and low first. It shows stock as it is now, whatever the period.");

    public static string Category => ReportsConstants.Categories.Commerce;

    public static Permission Permission => InventoryPermissions.ViewInventory;

    public async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var items = new List<StockItem>();

        for (var skip = 0; ; skip += PageSize)
        {
            var page = await _items.ListForExportAsync(skip, PageSize, cancellationToken);

            items.AddRange(page);

            if (page.Count < PageSize)
            {
                break;
            }
        }

        var ordered = items
            .OrderBy(item => item.Available > 0 ? 1 : 0)
            .ThenBy(item => item.IsLow ? 0 : 1)
            .ThenBy(item => item.Available)
            .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var document = new ReportDocument { Title = DisplayName.Value };

        document.Add(ReportSection.ForMetrics(S["Summary"].Value,
        [
            new ReportMetric(S["Items counted"].Value, ReportFormat.Number(items.Count)),
            new ReportMetric(S["Units on hand"].Value, ReportFormat.Number(items.Sum(item => (long)item.OnHand))),
            new ReportMetric(S["Units held for orders"].Value, ReportFormat.Number(items.Sum(item => (long)item.Reserved))),
            new ReportMetric(S["Out of stock"].Value, ReportFormat.Number(items.Count(item => item.Available <= 0))),
            new ReportMetric(S["Running low"].Value, ReportFormat.Number(items.Count(item => item.Available > 0 && item.IsLow))),
        ]));

        document.Add(ReportSection.ForTable(S["Items"].Value,
        [
            new ReportColumn(S["Item"].Value),
            new ReportColumn(S["SKU"].Value),
            new ReportColumn(S["On hand"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Held"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Available"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Status"].Value),
        ], [.. ordered.Select(item => new ReportRow(
        [
            item.Title ?? string.Empty,
            item.Sku ?? string.Empty,
            ReportFormat.Number(item.OnHand),
            ReportFormat.Number(item.Reserved),
            ReportFormat.Number(item.Available),
            item.Available <= 0
                ? (item.AllowBackorder ? S["Out of stock, backorders allowed"].Value : S["Out of stock"].Value)
                : item.IsLow ? S["Low"].Value : S["In stock"].Value,
        ]))]));

        return document;
    }
}
