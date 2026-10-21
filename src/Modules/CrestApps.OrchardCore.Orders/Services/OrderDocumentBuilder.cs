using System.Globalization;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Addresses.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Orders.Services;

/// <summary>
/// Builds an order's printable documents: its invoice, with what was charged, paid and is still owed, and its packing
/// slip, with what to put in the box and where it goes, for the whole order or one shipment.
/// </summary>
public sealed class OrderDocumentBuilder
{
    private readonly ISiteService _siteService;
    private readonly ILocalClock _localClock;
    private readonly OrderOperationsService _operations;
    private readonly IServiceProvider _serviceProvider;

    internal readonly IStringLocalizer S;

    public OrderDocumentBuilder(
        ISiteService siteService,
        ILocalClock localClock,
        OrderOperationsService operations,
        IServiceProvider serviceProvider,
        IStringLocalizer<OrderDocumentBuilder> stringLocalizer)
    {
        _siteService = siteService;
        _localClock = localClock;
        _operations = operations;
        _serviceProvider = serviceProvider;
        S = stringLocalizer;
    }

    public async Task<OrderDocumentViewModel> BuildInvoiceAsync(Order order, CancellationToken cancellationToken = default)
    {
        var document = await BuildAsync(order);
        var money = await _operations.GetMoneyAsync(order, cancellationToken);

        document.Title = S["Invoice"];
        document.Lines = [.. order.Lines.Select(line => new OrderDocumentLine
        {
            Description = OrderLineText.Describe(line),
            Sku = line.Sku,
            Quantity = line.Quantity,
            UnitPrice = line.UnitPrice,
            Amount = line.LineTotal,
        })];
        document.Discounts = [.. (order.Discounts ?? []).Where(discount => discount.Amount > 0m)
            .Select(discount => (string.IsNullOrEmpty(discount.Code) ? discount.Description : S["Discount ({0})", discount.Code].Value, discount.Amount))];
        document.AmountDue = money.Outstanding;
        document.Status = order.Status == OrderStatus.Canceled
            ? S["Canceled"]
            : money.Outstanding > 0m || order.Status == OrderStatus.AwaitingPayment
                ? S["Payment due"]
                : order.RefundStatus == OrderRefundStatus.Refunded
                    ? S["Refunded"]
                    : S["Paid"];

        return document;
    }

    /// <summary>
    /// Builds a packing slip: the whole order's goods, or a shipment's lines when <paramref name="shipped"/> names them,
    /// by line id and quantity.
    /// </summary>
    public async Task<OrderDocumentViewModel> BuildPackingSlipAsync(Order order, IReadOnlyDictionary<string, int> shipped = null, string shipmentNumber = null)
    {
        var document = await BuildAsync(order);

        document.Title = S["Packing slip"];
        document.ShipmentNumber = shipmentNumber;
        document.Lines = [.. order.Lines
            .Where(line => shipped is null ? line.ProductType != Products.Core.Models.ProductType.Digital && line.ProductType != Products.Core.Models.ProductType.Service : shipped.ContainsKey(line.LineId))
            .Select(line => new OrderDocumentLine
            {
                Description = OrderLineText.DescribeForPacking(line),
                Sku = line.Sku,
                Quantity = shipped?.GetValueOrDefault(line.LineId) ?? line.Quantity,
            })
            .Where(line => line.Quantity > 0)];

        return document;
    }

    /// <summary>
    /// Reads "lineId:quantity" pairs, such as a shipment's lines in a packing slip link.
    /// </summary>
    public static Dictionary<string, int> ParseLines(string lines)
    {
        if (string.IsNullOrWhiteSpace(lines))
        {
            return null;
        }

        var result = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var pair in lines.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = pair.LastIndexOf(':');

            if (separator > 0 && int.TryParse(pair[(separator + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var quantity) && quantity > 0)
            {
                result[pair[..separator]] = quantity;
            }
        }

        return result.Count == 0 ? null : result;
    }

    private async Task<OrderDocumentViewModel> BuildAsync(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);

        var site = await _siteService.GetSiteSettingsAsync();
        var formatter = _serviceProvider.GetService<IAddressFormatter>();

        return new OrderDocumentViewModel
        {
            Order = order,
            Header = ReadHeader(site),
            Placed = await LocalAsync(order.CreatedUtc),
            Paid = order.PaidUtc is { } paid ? await LocalAsync(paid) : null,
            BilledTo = order.BillingAddress is null || formatter is null ? [] : [.. await formatter.GetLinesAsync(order.BillingAddress)],
            ShipsTo = order.ShippingAddress is null || formatter is null ? [] : [.. await formatter.GetLinesAsync(order.ShippingAddress)],
        };
    }

    // The store's name and contact as the receipts show them (Settings › Receipts), else the site's name. They're read
    // from the site settings so orders print the same header without depending on the Receipts module.
    private static OrderDocumentHeader ReadHeader(ISite site)
    {
        var receipts = site.Properties?["ReceiptSettings"] as JsonObject;

        string Read(string name)
            => receipts?[name] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) ? text.Trim() : null;

        return new OrderDocumentHeader
        {
            Name = Read("BusinessName") ?? site.SiteName,
            LogoUrl = Read("LogoUrl"),
            Address = Read("BusinessAddress"),
            Email = Read("ContactEmail"),
            Phone = Read("ContactPhone"),
            Website = Read("Website"),
            Footer = Read("FooterText"),
        };
    }

    private async Task<DateTime> LocalAsync(DateTime utc)
        => (await _localClock.ConvertToLocalAsync(DateTime.SpecifyKind(utc, DateTimeKind.Utc))).DateTime;
}
