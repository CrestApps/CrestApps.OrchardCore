using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.DataSources;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Products.Reports.DataSources;

/// <summary>
/// One row per named price a product is offered at, such as a monthly and an annual plan or a one-time purchase: its
/// amount in its own currency, whether it is charged once or repeats and how often, its trial and setup fee, and when it
/// is offered.
/// </summary>
public sealed class ProductPricesReportDataSet : ProductEntryReportDataSet<ProductPrice>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProductPricesReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session products are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="access">Decides which products the principal may read.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ProductPricesReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        ProductReportAccess access,
        IStringLocalizer<ProductPricesReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.ProductSource.Prices,
                stringLocalizer["Product prices"],
                stringLocalizer["One row per named price a product is offered at: its amount in its own currency, whether it repeats and how often, its trial, its setup fee and when it is offered."]),
            session,
            authorizationService,
            access,
            ProductIdField)
    {
        var S = stringLocalizer;
        var product = S["Product"].Value;
        var price = S["Price"].Value;
        var inCurrency = S["In the price's currency."].Value;

        AddProductFields(ProductIdField, S["Product ID"], "ProductTitle", S["Product"], S["Product type"], S["Published"], product);

        AddField(nameof(ProductPrice.PriceId), S["Price ID"], DataFieldType.Text, row => row.Child.PriceId, price).Identifier();
        AddField("Position", S["Price number"], DataFieldType.Integer, row => row.Position, price);
        AddField("PriceName", S["Price"], DataFieldType.Text, row => row.Child.Name, price);
        AddField(nameof(ProductPrice.Kind), S["Charged"], DataFieldType.Text, row => row.Child.Kind, price).Description = S["OneTime for a price charged once, Recurring for one that repeats."].Value;
        AddField(nameof(ProductPrice.Currency), S["Currency"], DataFieldType.Text, row => row.Child.Currency, price);
        AddField(nameof(ProductPrice.Amount), S["Amount"], DataFieldType.Decimal, row => row.Child.Amount, price).Description = inCurrency;
        AddField(nameof(ProductPrice.BillingDuration), S["Billing every"], DataFieldType.Integer, row => row.Child.BillingDuration, price).Description = S["The number of billing units in one cycle of a recurring price."].Value;
        AddField(nameof(ProductPrice.Interval), S["Billing unit"], DataFieldType.Text, row => row.Child.Interval, price);
        AddField(nameof(ProductPrice.BillingCycleLimit), S["Cycles"], DataFieldType.Integer, row => row.Child.BillingCycleLimit, price).Description = S["The number of cycles billed before the agreement ends. Empty when it bills until it is canceled."].Value;
        AddField(nameof(ProductPrice.TrialDays), S["Trial days"], DataFieldType.Integer, row => row.Child.TrialDays, price);
        AddField(nameof(ProductPrice.SetupFee), S["Setup fee"], DataFieldType.Decimal, row => row.Child.SetupFee, price).Description = inCurrency;
        AddField(nameof(ProductPrice.AllowCustomAmount), S["Buyer names the amount"], DataFieldType.Boolean, row => row.Child.AllowCustomAmount, price);
        AddField(nameof(ProductPrice.MinimumAmount), S["Least amount"], DataFieldType.Decimal, row => row.Child.MinimumAmount, price).Description = inCurrency;
        AddField(nameof(ProductPrice.MaximumAmount), S["Most amount"], DataFieldType.Decimal, row => row.Child.MaximumAmount, price).Description = inCurrency;
        AddField(nameof(ProductPrice.IsDefault), S["Default"], DataFieldType.Boolean, row => row.Child.IsDefault, price);
        AddField(nameof(ProductPrice.IsActive), S["Active"], DataFieldType.Boolean, row => row.Child.IsActive, price);
        AddField(nameof(ProductPrice.EffectiveFromUtc), S["Offered from"], DataFieldType.DateTime, row => row.Child.EffectiveFromUtc, price);
        AddField(nameof(ProductPrice.EffectiveToUtc), S["Offered until"], DataFieldType.DateTime, row => row.Child.EffectiveToUtc, price);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override IEnumerable<ProductPrice> GetEntries(ContentItem product)
    {
        return ProductReportValues.Part<ProductPricePart>(product)?.Prices;
    }
}
