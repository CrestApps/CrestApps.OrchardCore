using CrestApps.OrchardCore.Commerce.Api;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.PointOfSale.Api;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Receipts;
using CrestApps.OrchardCore.Receipts.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.PointOfSale.Services;

/// <summary>
/// A register sale's receipt: as data for a till app to print in its own layout, and as the branded HTML the Receipts
/// settings give, to print or email.
/// </summary>
internal static class PosReceipts
{
    public static ApiPosReceipt ToApi(Order order, decimal change)
    {
        var currency = order.Currency;

        return new ApiPosReceipt(
            order.ItemId,
            order.OrderNumber,
            order.PaidUtc ?? order.CreatedUtc,
            order.StaffName,
            order.Lines.Select(line => new ApiPosReceiptLine(OrderLineText.Describe(line), line.Quantity, ApiMoney.From(line.UnitPrice, currency), ApiMoney.From(line.LineTotal, currency))).ToList(),
            ApiMoney.From(order.Subtotal, currency),
            ApiMoney.From(order.DiscountTotal, currency),
            ApiMoney.From(order.TaxAmount ?? 0m, currency),
            ApiMoney.From(order.Total ?? order.Subtotal, currency),
            GetPayments(order, "Cash", "Card").Select(payment => new ApiPosReceiptPayment(payment.Method, ApiMoney.From(payment.Amount, currency))).ToList(),
            ApiMoney.From(change, currency));
    }

    /// <summary>
    /// Returns the receipt as HTML, or <see langword="null"/> without the Receipts feature.
    /// </summary>
    public static async Task<string> RenderAsync(Order order, IServiceProvider services)
    {
        if (services.GetService<IReceiptService>() is not { } receipts || services.GetService<IReceiptHtmlRenderer>() is not { } renderer)
        {
            return null;
        }

        var S = services.GetRequiredService<IStringLocalizerFactory>().Create(typeof(PosReceipts));
        var document = await receipts.BuildAsync(new ReceiptRequest
        {
            BilledToName = order.CustomerName,
            BilledToEmail = order.CustomerEmail,
            Reference = order.OrderNumber,
            SourceLabel = S["Point of sale"],
            IssuedAt = order.PaidUtc ?? order.CreatedUtc,
            Currency = order.Currency,
            LineItems = [.. order.Lines.Select(line => new ReceiptLineItem
            {
                Description = OrderLineText.Describe(line),
                Quantity = line.Quantity,
                UnitAmount = line.UnitPrice,
                Amount = line.LineTotal,
            })],
            TaxAmount = order.TaxAmount ?? 0m,
            Total = order.Total ?? order.Subtotal,
            Status = order.Status == OrderStatus.Paid ? ReceiptStatus.Paid : ReceiptStatus.Due,
            Notes = string.Join(" · ", GetPayments(order, S["Cash"], S["Card"]).Select(payment => $"{payment.Method}: {ProductPriceFormatter.Format(payment.Amount, order.Currency)}")),
        });

        return renderer.Render(document);
    }

    // How the order was paid: each balance it was tendered, then the card for the rest. The API names the methods in
    // English, for the till app to show as it likes; the printed receipt names them in the store's language.
    private static IEnumerable<(string Method, decimal Amount)> GetPayments(Order order, string cash, string card)
    {
        var total = order.Total ?? order.Subtotal;
        var tendered = 0m;

        foreach (var tender in order.Tenders ?? [])
        {
            tendered += tender.Amount;

            yield return (tender.ProviderKey == PosConstants.CashTenderKey ? cash : tender.Description ?? tender.ProviderKey, tender.Amount);
        }

        if (total - tendered > 0m)
        {
            yield return (card, total - tendered);
        }
    }
}
