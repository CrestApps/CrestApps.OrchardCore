using System.Globalization;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Shipping.Indexes;
using CrestApps.OrchardCore.Shipping.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Shipping.Reports;

/// <summary>
/// How fast the store ships: the shipments sent in the period, how long each order waited from payment to shipping, how
/// long carriers took to deliver, by carrier, and the slowest shipments.
/// </summary>
public sealed class ShippingPerformanceReport : IReport
{
    private const int PageSize = 500;
    private const int Slowest = 15;

    private static readonly DateTime _earliest = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly ISession _session;
    private readonly IOrderStore _orderStore;
    private readonly ISiteService _siteService;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public ShippingPerformanceReport(ISession session, IOrderStore orderStore, ISiteService siteService, IClock clock, IStringLocalizer<ShippingPerformanceReport> stringLocalizer)
    {
        _session = session;
        _orderStore = orderStore;
        _siteService = siteService;
        _clock = clock;
        S = stringLocalizer;
    }

    public string Name => "shipping-performance";

    public LocalizedString DisplayName => S["Shipping times"];

    public LocalizedString Description => S["The shipments sent in the period: how long orders waited from payment to shipping, how long carriers took to deliver, by carrier, and the slowest shipments."];

    public string Category => ReportsConstants.Categories.Commerce;

    public Permission Permission => ShippingPermissions.FulfillOrders;

    public async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var range = context.Filter.GetDateRange();
        var from = range.FromUtc ?? _earliest;
        var to = range.ToUtc ?? _clock.UtcNow;
        var shipments = new List<Shipment>();

        for (var skip = 0; ; skip += PageSize)
        {
            var page = (await _session
                .Query<Shipment, ShipmentIndex>(index => index.ShippedUtc != null && index.ShippedUtc >= from && index.ShippedUtc < to, collection: ShippingConstants.CollectionName)
                .OrderBy(index => index.Id)
                .Skip(skip)
                .Take(PageSize)
                .ListAsync(cancellationToken)).ToList();

            shipments.AddRange(page);

            if (page.Count < PageSize)
            {
                break;
            }
        }

        var paidUtc = new Dictionary<string, DateTime?>(StringComparer.Ordinal);

        foreach (var orderId in shipments.Select(shipment => shipment.OrderId).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal))
        {
            var order = await _orderStore.FindByIdAsync(orderId, cancellationToken);
            paidUtc[orderId] = order?.PaidUtc ?? order?.CreatedUtc;
        }

        var timeZone = await GetTimeZoneAsync();
        var times = ShippingTimes.Measure(shipments, orderId => paidUtc.TryGetValue(orderId ?? string.Empty, out var paid) ? paid : null);
        var document = new ReportDocument { Title = DisplayName.Value };

        document.Add(ReportSection.ForMetrics(S["Summary"].Value,
        [
            new ReportMetric(S["Shipments sent"].Value, ReportFormat.Number(times.Count)),
            new ReportMetric(S["Orders shipped"].Value, ReportFormat.Number(times.Select(time => time.Shipment.OrderId).Distinct(StringComparer.Ordinal).Count())),
            new ReportMetric(S["Average time to ship"].Value, Days(ShippingTimes.Average(times.Select(time => time.ToShip))), S["From the order's payment to the shipment leaving."].Value),
            new ReportMetric(S["Median time to ship"].Value, Days(ShippingTimes.Median(times.Select(time => time.ToShip)))),
            new ReportMetric(S["Shipped within 2 days"].Value, Share(times.Count(time => time.ToShip <= TimeSpan.FromDays(2)), times.Count(time => time.ToShip.HasValue))),
            new ReportMetric(S["Delivered"].Value, ReportFormat.Number(times.Count(time => time.Shipment.DeliveredUtc.HasValue))),
            new ReportMetric(S["Average delivery time"].Value, Days(ShippingTimes.Average(times.Select(time => time.ToDeliver))), S["From leaving to delivered, for the shipments marked delivered."].Value),
            new ReportMetric(S["Returned"].Value, ReportFormat.Number(times.Count(time => time.Shipment.ReturnedUtc.HasValue))),
        ]));

        if (times.Count == 0)
        {
            return document;
        }

        document.Add(ReportSection.ForTable(S["By carrier"].Value,
        [
            new ReportColumn(S["Carrier"].Value),
            new ReportColumn(S["Shipments"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Average time to ship"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Delivered"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Average delivery time"].Value, ReportColumnAlign.End),
        ], [.. times
            .GroupBy(time => string.IsNullOrWhiteSpace(time.Shipment.Carrier) ? string.Empty : time.Shipment.Carrier.Trim(), StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .Select(group => new ReportRow(
            [
                string.IsNullOrEmpty(group.Key) ? S["No carrier given"].Value : group.Key,
                ReportFormat.Number(group.Count()),
                Days(ShippingTimes.Average(group.Select(time => time.ToShip))),
                ReportFormat.Number(group.Count(time => time.Shipment.DeliveredUtc.HasValue)),
                Days(ShippingTimes.Average(group.Select(time => time.ToDeliver))),
            ]))]));

        document.Add(ReportSection.ForTable(S["Slowest to ship"].Value,
        [
            new ReportColumn(S["Order"].Value),
            new ReportColumn(S["Shipment"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Paid"].Value),
            new ReportColumn(S["Shipped"].Value),
            new ReportColumn(S["Time to ship"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Carrier"].Value),
        ], [.. times
            .Where(time => time.ToShip.HasValue)
            .OrderByDescending(time => time.ToShip)
            .Take(Slowest)
            .Select(time => new ReportRow(
            [
                time.Shipment.OrderNumber ?? string.Empty,
                ReportFormat.Number(time.Shipment.Number),
                time.PaidUtc is { } paid ? Day(paid, timeZone) : string.Empty,
                Day(time.Shipment.ShippedUtc.Value, timeZone),
                Days(time.ToShip),
                time.Shipment.Carrier ?? string.Empty,
            ]))]));

        return document;
    }

    private string Days(TimeSpan? span)
        => span is not { } value ? S["n/a"].Value
            : value.TotalDays >= 1 ? S["{0} days", value.TotalDays.ToString("0.0", CultureInfo.CurrentCulture)].Value
            : S["{0} hours", value.TotalHours.ToString("0.0", CultureInfo.CurrentCulture)].Value;

    private static string Share(int part, int whole)
        => whole == 0 ? "0%" : ((double)part / whole).ToString("P0", CultureInfo.CurrentCulture);

    private static string Day(DateTime utc, TimeZoneInfo timeZone)
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), timeZone).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private async Task<TimeZoneInfo> GetTimeZoneAsync()
    {
        var site = await _siteService.GetSiteSettingsAsync();

        return !string.IsNullOrEmpty(site?.TimeZoneId) && TimeZoneInfo.TryFindSystemTimeZoneById(site.TimeZoneId, out var timeZone)
            ? timeZone
            : TimeZoneInfo.Utc;
    }
}

/// <summary>
/// The arithmetic behind the shipping times report, kept apart from reading shipments so it can be checked on its own.
/// </summary>
public static class ShippingTimes
{
    /// <summary>
    /// Measures each sent shipment: from its order's payment to leaving, and from leaving to delivered.
    /// </summary>
    public static IReadOnlyList<ShipmentTime> Measure(IEnumerable<Shipment> shipments, Func<string, DateTime?> paidUtc)
        => [.. shipments
            .Where(shipment => shipment.ShippedUtc.HasValue)
            .Select(shipment =>
            {
                var paid = paidUtc(shipment.OrderId);
                var shipped = shipment.ShippedUtc.Value;

                return new ShipmentTime(
                    shipment,
                    paid,
                    paid is { } value && shipped >= value ? shipped - value : null,
                    shipment.DeliveredUtc is { } delivered && delivered >= shipped ? delivered - shipped : null);
            })];

    public static TimeSpan? Average(IEnumerable<TimeSpan?> spans)
    {
        var values = spans.Where(span => span.HasValue).Select(span => span.Value.Ticks).ToArray();

        return values.Length == 0 ? null : TimeSpan.FromTicks((long)values.Average());
    }

    public static TimeSpan? Median(IEnumerable<TimeSpan?> spans)
    {
        var values = spans.Where(span => span.HasValue).Select(span => span.Value.Ticks).Order().ToArray();

        if (values.Length == 0)
        {
            return null;
        }

        var middle = values.Length / 2;

        return TimeSpan.FromTicks(values.Length % 2 == 1 ? values[middle] : (values[middle - 1] + values[middle]) / 2);
    }
}

/// <summary>
/// One sent shipment and how long it took.
/// </summary>
public sealed record ShipmentTime(Shipment Shipment, DateTime? PaidUtc, TimeSpan? ToShip, TimeSpan? ToDeliver);
