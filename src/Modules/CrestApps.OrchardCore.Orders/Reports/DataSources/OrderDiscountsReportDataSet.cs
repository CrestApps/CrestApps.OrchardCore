using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Orders.Reports.DataSources;

/// <summary>
/// One row per discount an order was given, such as a coupon: its code, what it took off, and what it applied to.
/// </summary>
public sealed class OrderDiscountsReportDataSet : OrderEntryReportDataSet<OrderDiscount>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OrderDiscountsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session orders are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OrderDiscountsReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<OrderDiscountsReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.OrderSource.OrderDiscounts,
                stringLocalizer["Order discounts"],
                stringLocalizer["One row per discount an order was given, such as a coupon: its code, what it took off in the order's currency, and what it applied to."]),
            session,
            authorizationService,
            stringLocalizer)
    {
        var S = stringLocalizer;
        var discount = S["Discount"].Value;

        AddField(nameof(OrderDiscount.Code), S["Code"], DataFieldType.Text, row => row.Child.Code, discount);
        AddField(nameof(OrderDiscount.Description), S["Description"], DataFieldType.Text, row => row.Child.Description, discount);
        AddField(nameof(OrderDiscount.Amount), S["Amount"], DataFieldType.Decimal, row => row.Child.Amount, discount).Description = S["In the order's currency."].Value;
        AddField("DiscountProvider", S["Given by"], DataFieldType.Text, row => row.Child.ProviderKey, discount);
        AddField(nameof(OrderDiscount.Target), S["Applies to"], DataFieldType.Text, row => row.Child.Target, discount);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override IEnumerable<OrderDiscount> GetEntries(Order order)
    {
        return order.Discounts;
    }
}
