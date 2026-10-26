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
/// One row per use of a coupon code at a checkout, as a coupon's page lists its recent uses: the code, what it took off,
/// who used it, what it was used for, and whether it was held, used, let go or given back. The discount is in the
/// currency of the checkout, which the <c>Currency</c> field names.
/// </summary>
public sealed class CouponRedemptionsReportDataSet : IndexedReportDataSet<CouponRedemption, CouponRedemptionIndex>, ICheckoutReportDataSet
{
    /// <summary>
    /// The name of the field that holds when the code was put on the checkout, the data set's main date.
    /// </summary>
    public const string CreatedUtcField = "CreatedUtc";

    /// <summary>
    /// The name of the field that holds the account of the buyer who used the code.
    /// </summary>
    public const string UserIdField = "UserId";

    /// <summary>
    /// Initializes a new instance of the <see cref="CouponRedemptionsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session the uses are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CouponRedemptionsReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<CouponRedemptionsReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.CheckoutSource.CouponRedemptions,
                stringLocalizer["Coupon uses"],
                stringLocalizer["One row per use of a coupon code at a checkout: the code, the discount in the checkout's currency, who used it, what for, and whether it was used or given back."]),
            session,
            authorizationService,
            CheckoutConstants.CouponCollectionName,
            CheckoutPermissions.ManageCoupons)
    {
        var S = stringLocalizer;
        var use = S["Use"].Value;
        var dates = S["Dates"].Value;

        AddField(CommerceReportDataSets.ItemIdField, S["Use ID"], DataFieldType.Text, record => record.ItemId, use).Identifier();
        AddField(nameof(CouponRedemption.Code), S["Code"], DataFieldType.Text, record => record.Code, use);
        AddField(nameof(CouponRedemption.Status), S["Status"], DataFieldType.Text, record => record.Status, use);
        AddField(nameof(CouponRedemption.IsCounted), S["Counts toward the limits"], DataFieldType.Boolean, record => record.IsCounted, use);
        AddField(nameof(CouponRedemption.Currency), S["Currency"], DataFieldType.Text, record => record.Currency, use);
        AddField(nameof(CouponRedemption.Amount), S["Discount"], DataFieldType.Decimal, record => record.Amount, use)
            .Description = S["What the code took off, in the checkout's currency."].Value;
        AddField(nameof(CouponRedemption.SessionId), S["Checkout session ID"], DataFieldType.Text, record => record.SessionId, use)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.CheckoutSource.Name, CommerceReportDataSets.CheckoutSource.CheckoutSessions));
        AddField(nameof(CouponRedemption.ReferenceType), S["Used for"], DataFieldType.Text, record => record.ReferenceType, use);
        AddField(nameof(CouponRedemption.ReferenceId), S["Reference ID"], DataFieldType.Text, record => record.ReferenceId, use).Identifier();
        AddField("OrderId", S["Order ID"], DataFieldType.Text, record => string.Equals(record.ReferenceType, CheckoutReferenceTypes.Order, StringComparison.Ordinal) ? record.ReferenceId : null, use)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.OrderSource.Name, CommerceReportDataSets.OrderSource.Orders));
        AddField(UserIdField, S["User ID"], DataFieldType.Text, record => record.CustomerId, use).Identifier(ReportDataReferences.User());

        AddField(CreatedUtcField, S["Put on the checkout"], DataFieldType.DateTime, record => record.CreatedUtc, dates);
        AddField(nameof(CouponRedemption.ExpiresUtc), S["Hold expires"], DataFieldType.DateTime, record => record.ExpiresUtc, dates);
        AddField(nameof(CouponRedemption.RedeemedUtc), S["Used"], DataFieldType.DateTime, record => record.RedeemedUtc, dates);
        AddField(nameof(CouponRedemption.EndedUtc), S["Let go or given back"], DataFieldType.DateTime, record => record.EndedUtc, dates);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override (string Field, Expression<Func<CouponRedemptionIndex, DateTime>> Column)? DateColumn
        => (CreatedUtcField, index => index.CreatedUtc);

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<CouponRedemptionIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<CouponRedemptionIndex, string>>>(StringComparer.Ordinal)
        {
            [CommerceReportDataSets.ItemIdField] = index => index.ItemId,
            [nameof(CouponRedemption.SessionId)] = index => index.SessionId,
            [UserIdField] = index => index.CustomerId,
        };

    /// <inheritdoc/>
    protected override void DescribeColumns(ReportIndexConditions<CouponRedemptionIndex> columns)
    {
        // The index keeps the name of the status, as the field reads it.
        columns.Text(nameof(CouponRedemption.Status), nameof(CouponRedemptionIndex.Status));
    }

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, string> GetAggregateColumns()
    {
        // The buyer's account is kept in a column of another name.
        var columns = new Dictionary<string, string>(base.GetAggregateColumns(), StringComparer.Ordinal)
        {
            [UserIdField] = nameof(CouponRedemptionIndex.CustomerId),
        };

        return columns;
    }
}
