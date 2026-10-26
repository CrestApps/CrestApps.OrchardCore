using System.Linq.Expressions;
using CrestApps.OrchardCore.Carts.Core;
using CrestApps.OrchardCore.Carts.Core.Indexes;
using CrestApps.OrchardCore.Carts.Core.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Carts.Reports.DataSources;

/// <summary>
/// One row per open cart: whose it is, what is in it, what that was worth when it was added, and when it was last
/// touched and expires. Money is in the cart's currency, which the <c>Currency</c> field names. The token that finds a
/// guest's cart is never exposed.
/// </summary>
/// <remarks>
/// The carts index has no creation date, so a date range on <see cref="CreatedUtcField"/> is applied to the rows read
/// rather than to the index: carts are read newest first by their index rows.
/// </remarks>
public sealed class CartsReportDataSet : IndexedReportDataSet<Cart, CartIndex>, ICartReportDataSet
{
    /// <summary>
    /// The name of the field that holds when the cart was started, the data set's main date.
    /// </summary>
    public const string CreatedUtcField = "CreatedUtc";

    /// <summary>
    /// Initializes a new instance of the <see cref="CartsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session carts are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CartsReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<CartsReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.CartSource.Carts,
                stringLocalizer["Carts"],
                stringLocalizer["One row per open cart: whose it is, its lines and units, their value when added in the cart's currency, and when it was started, last changed and expires."]),
            session,
            authorizationService,
            CartConstants.CollectionName,
            CartReportPermissions.ManageOrders)
    {
        var S = stringLocalizer;
        var cart = S["Cart"].Value;
        var dates = S["Dates"].Value;

        AddField(CommerceReportDataSets.ItemIdField, S["Cart ID"], DataFieldType.Text, record => record.ItemId, cart).Identifier();
        AddField(nameof(Cart.UserId), S["User ID"], DataFieldType.Text, record => record.UserId, cart).Identifier(ReportDataReferences.User());
        AddField("IsGuest", S["Guest"], DataFieldType.Boolean, record => string.IsNullOrEmpty(record.UserId), cart);
        AddField("LineCount", S["Lines"], DataFieldType.Integer, record => record.Lines?.Count ?? 0, cart);
        AddField(nameof(Cart.ItemCount), S["Units"], DataFieldType.Integer, record => record.ItemCount, cart);
        AddField(nameof(Cart.Currency), S["Currency"], DataFieldType.Text, record => record.Currency, cart);
        AddField("Value", S["Value when added"], DataFieldType.Decimal, record => record.Lines?.Sum(line => line.AddedUnitPrice * line.Quantity) ?? 0m, cart)
            .Description = S["The units at the prices they had when they were added, in the cart's currency. It is never charged."].Value;

        AddField(CreatedUtcField, S["Started"], DataFieldType.DateTime, record => record.CreatedUtc, dates);
        AddField(nameof(Cart.ModifiedUtc), S["Last changed"], DataFieldType.DateTime, record => record.ModifiedUtc, dates);
        AddField(nameof(Cart.ExpiresUtc), S["Expires"], DataFieldType.DateTime, record => record.ExpiresUtc, dates);

        CompleteDescription();

        // The main date is not in the index, so it is the default date without being pushed down.
        Descriptor.DefaultDateField = CreatedUtcField;
    }

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<CartIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<CartIndex, string>>>(StringComparer.Ordinal)
        {
            [CommerceReportDataSets.ItemIdField] = index => index.ItemId,
            [nameof(Cart.UserId)] = index => index.UserId,
        };
}
