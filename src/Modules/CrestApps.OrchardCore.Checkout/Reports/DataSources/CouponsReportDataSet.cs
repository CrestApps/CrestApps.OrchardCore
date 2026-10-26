using System.Linq.Expressions;
using CrestApps.OrchardCore.Checkout.Core;
using CrestApps.OrchardCore.Checkout.Core.Indexes;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Checkout.Reports.DataSources;

/// <summary>
/// One row per coupon, as the coupons page lists it: its code, what it takes off, its limits and how often it was used.
/// A fixed amount off is in the coupon's currency, which the <c>Currency</c> field names. The customer a coupon is
/// limited to is never exposed.
/// </summary>
public sealed class CouponsReportDataSet : IndexedReportDataSet<Coupon, CouponIndex>, ICheckoutReportDataSet
{
    /// <summary>
    /// The name of the field that holds when the coupon was created, the data set's main date.
    /// </summary>
    public const string CreatedUtcField = "CreatedUtc";

    /// <summary>
    /// Initializes a new instance of the <see cref="CouponsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session coupons are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CouponsReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<CouponsReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.CheckoutSource.Coupons,
                stringLocalizer["Coupons"],
                stringLocalizer["One row per coupon: its code, what it takes off, when it runs, its limits and how often it was used."]),
            session,
            authorizationService,
            CheckoutConstants.CouponCollectionName,
            CheckoutPermissions.ManageCoupons)
    {
        var S = stringLocalizer;
        var coupon = S["Coupon"].Value;
        var discount = S["Discount"].Value;
        var limits = S["Limits"].Value;
        var dates = S["Dates"].Value;

        AddField(CommerceReportDataSets.ItemIdField, S["Coupon ID"], DataFieldType.Text, record => record.ItemId, coupon).Identifier();
        AddField(nameof(Coupon.Code), S["Code"], DataFieldType.Text, record => record.Code, coupon);
        AddField(nameof(Coupon.Description), S["Description"], DataFieldType.Text, record => record.Description, coupon);
        AddField(nameof(Coupon.IsEnabled), S["Enabled"], DataFieldType.Boolean, record => record.IsEnabled, coupon);
        AddField(nameof(Coupon.IsAutomatic), S["Applies by itself"], DataFieldType.Boolean, record => record.IsAutomatic, coupon);
        AddField(nameof(Coupon.OnlyGeneratedCodes), S["Only generated codes"], DataFieldType.Boolean, record => record.OnlyGeneratedCodes, coupon);
        AddField("IsLimited", S["Limited to some buyers"], DataFieldType.Boolean, record => record.IsScoped, coupon)
            .Description = S["Whether the coupon is limited to sources, kinds of purchase or one customer."].Value;

        AddField(nameof(Coupon.Kind), S["Kind"], DataFieldType.Text, record => record.Kind, discount);
        AddField(nameof(Coupon.Target), S["Applies to"], DataFieldType.Text, record => record.Target, discount);
        AddField(nameof(Coupon.Percentage), S["Percentage off"], DataFieldType.Decimal, record => record.Percentage, discount);
        AddField(nameof(Coupon.Currency), S["Currency"], DataFieldType.Text, record => record.Currency, discount);
        AddField(nameof(Coupon.Amount), S["Amount off"], DataFieldType.Decimal, record => record.Amount, discount)
            .Description = S["A fixed amount taken off, in the coupon's currency."].Value;
        AddField(nameof(Coupon.MinimumAmount), S["Minimum spend"], DataFieldType.Decimal, record => record.MinimumAmount, discount)
            .Description = S["What a checkout must come to before the coupon applies, in that checkout's own currency."].Value;
        AddField(nameof(Coupon.MinimumQuantity), S["Minimum quantity"], DataFieldType.Integer, record => record.MinimumQuantity, discount);

        AddField(nameof(Coupon.MaxRedemptions), S["Most uses"], DataFieldType.Integer, record => record.MaxRedemptions, limits);
        AddField(nameof(Coupon.MaxRedemptionsPerCustomer), S["Most uses per customer"], DataFieldType.Integer, record => record.MaxRedemptionsPerCustomer, limits);
        AddField(nameof(Coupon.RedemptionCount), S["Times used"], DataFieldType.Integer, record => record.RedemptionCount, limits);

        AddField(nameof(Coupon.StartsUtc), S["Starts"], DataFieldType.DateTime, record => record.StartsUtc, dates);
        AddField(nameof(Coupon.EndsUtc), S["Ends"], DataFieldType.DateTime, record => record.EndsUtc, dates);
        AddField(CreatedUtcField, S["Created"], DataFieldType.DateTime, record => record.CreatedUtc, dates);
        AddField(nameof(Coupon.UpdatedUtc), S["Last changed"], DataFieldType.DateTime, record => record.UpdatedUtc, dates);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override (string Field, Expression<Func<CouponIndex, DateTime>> Column)? DateColumn
        => (CreatedUtcField, index => index.CreatedUtc);

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<CouponIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<CouponIndex, string>>>(StringComparer.Ordinal)
        {
            [CommerceReportDataSets.ItemIdField] = index => index.ItemId,
        };
}
