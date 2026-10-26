using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Taxation.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Orders.Reports.DataSources;

/// <summary>
/// One row per tax an order was charged, as its checkout worked it out: the jurisdiction, the tax and its rate, and what
/// it was charged on, so the tax collected can be added up by jurisdiction.
/// </summary>
public sealed class OrderTaxesReportDataSet : OrderEntryReportDataSet<TaxLine>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OrderTaxesReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session orders are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OrderTaxesReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<OrderTaxesReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.OrderSource.OrderTaxes,
                stringLocalizer["Order taxes"],
                stringLocalizer["One row per tax an order was charged: the jurisdiction, tax, rate, and the taxable and tax amounts in the order's currency."]),
            session,
            authorizationService,
            stringLocalizer)
    {
        var S = stringLocalizer;
        var tax = S["Tax"].Value;
        var inCurrency = S["In the order's currency."].Value;

        AddField(nameof(TaxLine.JurisdictionId), S["Jurisdiction ID"], DataFieldType.Text, row => row.Child.JurisdictionId, tax);
        AddField(nameof(TaxLine.JurisdictionName), S["Jurisdiction"], DataFieldType.Text, row => row.Child.JurisdictionName, tax);
        AddField(nameof(TaxLine.TaxCode), S["Tax code"], DataFieldType.Text, row => row.Child.TaxCode, tax);
        AddField(nameof(TaxLine.TaxName), S["Tax"], DataFieldType.Text, row => row.Child.TaxName, tax);
        AddField(nameof(TaxLine.TaxType), S["Tax type"], DataFieldType.Text, row => row.Child.TaxType, tax);
        AddField(nameof(TaxLine.Rate), S["Rate"], DataFieldType.Decimal, row => row.Child.Rate, tax);
        AddField(nameof(TaxLine.TaxableAmount), S["Taxable amount"], DataFieldType.Decimal, row => row.Child.TaxableAmount, tax).Description = inCurrency;
        AddField(nameof(TaxLine.TaxAmount), S["Tax amount"], DataFieldType.Decimal, row => row.Child.TaxAmount, tax).Description = inCurrency;
        AddField(nameof(TaxLine.Treatment), S["Treatment"], DataFieldType.Text, row => row.Child.Treatment, tax);
        AddField(nameof(TaxLine.IncludedInPrice), S["Included in the price"], DataFieldType.Boolean, row => row.Child.IncludedInPrice, tax);
        AddField(nameof(TaxLine.IsCompound), S["Compound"], DataFieldType.Boolean, row => row.Child.IsCompound, tax);
        AddField("BillingCountry", S["Billing country"], DataFieldType.Text, row => row.Parent.BillingAddress?.Country, tax);
        AddField("BillingRegion", S["Billing region"], DataFieldType.Text, row => row.Parent.BillingAddress?.Region, tax);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override IEnumerable<TaxLine> GetEntries(Order order)
    {
        return order.TaxLines;
    }
}
