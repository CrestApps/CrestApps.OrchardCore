using System.Linq.Expressions;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Indexes;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Orders.Reports.DataSources;

/// <summary>
/// One row per order, drafts and quotes included: what it came to, where it is, who placed it, and where it was made.
/// Money is in the order's own currency, which the <c>Currency</c> field names. The personal details are those the
/// order's admin page shows, and an anonymized order has none.
/// </summary>
public sealed class OrdersReportDataSet : IndexedReportDataSet<Order, OrderIndex>, IOrderReportDataSet
{
    /// <summary>
    /// The name of the field that holds when the order was placed, the data set's main date.
    /// </summary>
    public const string CreatedUtcField = "CreatedUtc";

    /// <summary>
    /// Initializes a new instance of the <see cref="OrdersReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session orders are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OrdersReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<OrdersReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.OrderSource.Orders,
                stringLocalizer["Orders"],
                stringLocalizer["One row per order, drafts and quotes included: its totals in its own currency, status, dates, customer and where it was made."]),
            session,
            authorizationService,
            OrderConstants.CollectionName,
            OrderPermissions.ManageOrders)
    {
        var S = stringLocalizer;
        var order = S["Order"].Value;
        var money = S["Money"].Value;
        var dates = S["Dates"].Value;
        var customer = S["Customer"].Value;
        var addresses = S["Addresses"].Value;
        var fulfillment = S["Fulfillment"].Value;
        var store = S["Store"].Value;
        var quote = S["Quote and approval"].Value;
        var inCurrency = S["In the order's currency."].Value;

        AddField(CommerceReportDataSets.ItemIdField, S["Order ID"], DataFieldType.Text, record => record.ItemId, order).Identifier();
        AddField(nameof(Order.OrderNumber), S["Order number"], DataFieldType.Text, record => record.OrderNumber, order);
        AddField(nameof(Order.Status), S["Status"], DataFieldType.Text, record => record.Status, order);
        AddField(nameof(Order.Channel), S["Sales channel"], DataFieldType.Text, record => record.Channel, order);
        AddField(nameof(Order.Culture), S["Culture"], DataFieldType.Text, record => record.Culture, order);
        AddField(nameof(Order.PurchaseOrderNumber), S["Purchase order number"], DataFieldType.Text, record => record.PurchaseOrderNumber, order);
        AddField("LineCount", S["Lines"], DataFieldType.Integer, record => record.Lines?.Count ?? 0, order);
        AddField(nameof(Order.ItemCount), S["Units"], DataFieldType.Integer, record => record.ItemCount, order);
        AddField(nameof(Order.CartId), S["Cart ID"], DataFieldType.Text, record => record.CartId, order)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.CartSource.Name, CommerceReportDataSets.CartSource.Carts));
        AddField(nameof(Order.CheckoutSessionId), S["Checkout session ID"], DataFieldType.Text, record => record.CheckoutSessionId, order)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.CheckoutSource.Name, CommerceReportDataSets.CheckoutSource.CheckoutSessions));

        AddField(nameof(Order.Currency), S["Currency"], DataFieldType.Text, record => record.Currency, money);
        AddField(nameof(Order.Subtotal), S["Subtotal"], DataFieldType.Decimal, record => record.Subtotal, money).Description = inCurrency;
        AddField(nameof(Order.DiscountTotal), S["Discounts"], DataFieldType.Decimal, record => record.DiscountTotal, money).Description = inCurrency;
        AddField("ShippingAmount", S["Shipping"], DataFieldType.Decimal, record => record.Shipping?.Amount, money).Description = inCurrency;
        AddField(nameof(Order.TaxAmount), S["Tax"], DataFieldType.Decimal, record => record.TaxAmount, money).Description = inCurrency;
        AddField(nameof(Order.Total), S["Total"], DataFieldType.Decimal, record => record.Total, money).Description = inCurrency;
        AddField("TenderedAmount", S["Paid from balances"], DataFieldType.Decimal, record => record.Tenders?.Sum(tender => tender.Amount) ?? 0m, money).Description = S["What gift cards and store credit paid, in the order's currency."].Value;
        AddField(nameof(Order.RefundedAmount), S["Refunded"], DataFieldType.Decimal, record => record.RefundedAmount, money).Description = inCurrency;
        AddField("NetAmount", S["Net"], DataFieldType.Decimal, record => record.Total.HasValue ? record.Total.Value - record.RefundedAmount : null, money).Description = S["The total less what was refunded, in the order's currency."].Value;
        AddField(nameof(Order.RefundStatus), S["Refund status"], DataFieldType.Text, record => record.RefundStatus, money);
        AddField(nameof(Order.HasOpenDispute), S["Has an open dispute"], DataFieldType.Boolean, record => record.HasOpenDispute, money);
        AddField("PaymentRisk", S["Payment risk"], DataFieldType.Text, record => record.PaymentRisk?.Level, money);

        AddField(CreatedUtcField, S["Placed"], DataFieldType.DateTime, record => record.CreatedUtc, dates);
        AddField(nameof(Order.PaidUtc), S["Paid"], DataFieldType.DateTime, record => record.PaidUtc, dates);
        AddField(nameof(Order.CanceledUtc), S["Canceled"], DataFieldType.DateTime, record => record.CanceledUtc, dates);
        AddField(nameof(Order.CancelReason), S["Cancel reason"], DataFieldType.Text, record => record.CancelReason, dates);
        AddField(nameof(Order.ModifiedUtc), S["Last changed"], DataFieldType.DateTime, record => record.ModifiedUtc, dates);
        AddField("IsAnonymized", S["Anonymized"], DataFieldType.Boolean, record => record.AnonymizedUtc.HasValue, dates);
        AddField(nameof(Order.RecoveryEmailSentUtc), S["Recovery email sent"], DataFieldType.DateTime, record => record.RecoveryEmailSentUtc, dates)
            .Description = S["When the buyer was emailed a link back to the order's checkout after leaving it unpaid."].Value;
        AddField("IsRecovered", S["Recovered"], DataFieldType.Boolean, record => record.RecoveryEmailSentUtc.HasValue && record.PaidUtc > record.RecoveryEmailSentUtc, dates)
            .Description = S["Whether the order was paid after the buyer was emailed a link back to its checkout."].Value;

        AddField(nameof(Order.CustomerId), S["Customer ID"], DataFieldType.Text, record => record.CustomerId, customer)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.CustomerSource.Name, CommerceReportDataSets.CustomerSource.Customers));
        AddField(nameof(Order.UserId), S["User ID"], DataFieldType.Text, record => record.UserId, customer).Identifier(ReportDataReferences.User());
        AddField("IsGuest", S["Guest"], DataFieldType.Boolean, record => string.IsNullOrEmpty(record.UserId), customer);
        AddField(nameof(Order.CustomerName), S["Customer name"], DataFieldType.Text, record => record.CustomerName, customer);
        AddField(nameof(Order.CustomerEmail), S["Customer email"], DataFieldType.Text, record => record.CustomerEmail, customer);
        AddField(nameof(Order.CompanyId), S["Company ID"], DataFieldType.Text, record => record.CompanyId, customer)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.CompanySource.Name, CommerceReportDataSets.CompanySource.Companies));

        AddField("BillingCountry", S["Billing country"], DataFieldType.Text, record => record.BillingAddress?.Country, addresses);
        AddField("BillingRegion", S["Billing region"], DataFieldType.Text, record => record.BillingAddress?.Region, addresses);
        AddField("BillingCity", S["Billing city"], DataFieldType.Text, record => record.BillingAddress?.City, addresses);
        AddField("BillingPostalCode", S["Billing postal code"], DataFieldType.Text, record => record.BillingAddress?.PostalCode, addresses);
        AddField("ShippingCountry", S["Shipping country"], DataFieldType.Text, record => record.ShippingAddress?.Country, addresses);
        AddField("ShippingRegion", S["Shipping region"], DataFieldType.Text, record => record.ShippingAddress?.Region, addresses);
        AddField("ShippingCity", S["Shipping city"], DataFieldType.Text, record => record.ShippingAddress?.City, addresses);
        AddField("ShippingPostalCode", S["Shipping postal code"], DataFieldType.Text, record => record.ShippingAddress?.PostalCode, addresses);

        AddField(nameof(Order.FulfillmentStatus), S["Fulfillment status"], DataFieldType.Text, record => record.FulfillmentStatus, fulfillment);
        AddField("ShippingMethod", S["Shipping method"], DataFieldType.Text, record => record.Shipping?.Name, fulfillment);
        AddField("ShippingProvider", S["Shipping provider"], DataFieldType.Text, record => record.Shipping?.ProviderName, fulfillment);
        AddField("IsPickup", S["Picked up"], DataFieldType.Boolean, record => record.Shipping?.IsPickup == true, fulfillment);

        AddField(nameof(Order.LocationId), S["Location ID"], DataFieldType.Text, record => record.LocationId, store)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.InventorySource.Name, CommerceReportDataSets.InventorySource.Locations));
        AddField(nameof(Order.RegisterId), S["Register ID"], DataFieldType.Text, record => record.RegisterId, store)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.PointOfSaleSource.Name, CommerceReportDataSets.PointOfSaleSource.Registers));
        AddField(nameof(Order.RegisterName), S["Register"], DataFieldType.Text, record => record.RegisterName, store);
        AddField(nameof(Order.ShiftId), S["Shift ID"], DataFieldType.Text, record => record.ShiftId, store)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.PointOfSaleSource.Name, CommerceReportDataSets.PointOfSaleSource.Shifts));
        AddField(nameof(Order.StaffUserId), S["Staff user ID"], DataFieldType.Text, record => record.StaffUserId, store).Identifier(ReportDataReferences.User());
        AddField(nameof(Order.StaffName), S["Staff member"], DataFieldType.Text, record => record.StaffName, store);

        AddField("QuoteStatus", S["Quote status"], DataFieldType.Text, record => record.Quote?.Status, quote);
        AddField("QuoteRequestedUtc", S["Quote requested"], DataFieldType.DateTime, record => record.Quote?.RequestedUtc, quote);
        AddField("QuoteSentUtc", S["Quote sent"], DataFieldType.DateTime, record => record.Quote?.SentUtc, quote);
        AddField("ApprovalStatus", S["Approval status"], DataFieldType.Text, record => record.Approval?.Status, quote);
        AddField("IsInvoice", S["Sent as an invoice"], DataFieldType.Boolean, record => record.Invoice is not null, quote);
        AddField("InvoiceDueUtc", S["Invoice due"], DataFieldType.DateTime, record => record.Invoice?.DueUtc, quote);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override (string Field, Expression<Func<OrderIndex, DateTime>> Column)? DateColumn
        => (CreatedUtcField, index => index.CreatedUtc);

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<OrderIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<OrderIndex, string>>>(StringComparer.Ordinal)
        {
            [CommerceReportDataSets.ItemIdField] = index => index.ItemId,
            [nameof(Order.CustomerId)] = index => index.CustomerId,
            [nameof(Order.UserId)] = index => index.UserId,
            [nameof(Order.CompanyId)] = index => index.CompanyId,
            [nameof(Order.CartId)] = index => index.CartId,
            [nameof(Order.CheckoutSessionId)] = index => index.CheckoutSessionId,
            [nameof(Order.LocationId)] = index => index.LocationId,
            [nameof(Order.ShiftId)] = index => index.ShiftId,
        };

    /// <inheritdoc/>
    protected override void DescribeColumns(ReportIndexConditions<OrderIndex> columns)
    {
        columns
            .Enum(nameof(Order.Status), index => index.Status)
            .Text(nameof(Order.Channel), nameof(OrderIndex.Channel))
            .Text(nameof(Order.OrderNumber), nameof(OrderIndex.OrderNumber));
    }

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, string> GetAggregateColumns()
    {
        // The index keeps the email address and the purchase order number in upper case, so neither is the field's value.
        var columns = new Dictionary<string, string>(base.GetAggregateColumns(), StringComparer.Ordinal);

        columns.Remove(nameof(Order.CustomerEmail));
        columns.Remove(nameof(Order.PurchaseOrderNumber));

        return columns;
    }
}
