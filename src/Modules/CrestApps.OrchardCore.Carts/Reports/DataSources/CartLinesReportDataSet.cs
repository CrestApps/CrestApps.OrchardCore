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
/// One row per product line of an open cart: the product or variant, how many, and the unit price it had when it was
/// added. Each row carries its cart's identifier, owner and currency, so a report can group the lines by them without
/// joining the carts. What the shopper typed for add-ons and line details is never exposed.
/// </summary>
public sealed class CartLinesReportDataSet : IndexedReportDataSet<Cart, CartIndex, ReportChildRow<Cart, CartLine>>, ICartReportDataSet
{
    /// <summary>
    /// The name of the field that holds the identifier of the cart a line belongs to.
    /// </summary>
    public const string CartIdField = "CartId";

    /// <summary>
    /// The name of the field that holds when the line was added, the data set's main date.
    /// </summary>
    public const string AddedUtcField = "AddedUtc";

    /// <summary>
    /// Initializes a new instance of the <see cref="CartLinesReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session carts are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CartLinesReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<CartLinesReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.CartSource.CartLines,
                stringLocalizer["Cart lines"],
                stringLocalizer["One row per product line of an open cart: the product or variant, the quantity, and the unit price it had when it was added, in the cart's currency."]),
            session,
            authorizationService,
            CartConstants.CollectionName,
            CartReportPermissions.ManageOrders)
    {
        var S = stringLocalizer;
        var cart = S["Cart"].Value;
        var line = S["Line"].Value;
        var inCurrency = S["In the cart's currency, as it was when the line was added. It is never charged."].Value;

        AddField(CartIdField, S["Cart ID"], DataFieldType.Text, row => row.Parent.ItemId, cart)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.CartSource.Name, CommerceReportDataSets.CartSource.Carts));
        AddField(nameof(Cart.UserId), S["User ID"], DataFieldType.Text, row => row.Parent.UserId, cart).Identifier(ReportDataReferences.User());
        AddField(nameof(Cart.Currency), S["Currency"], DataFieldType.Text, row => row.Parent.Currency, cart);
        AddField("CartModifiedUtc", S["Cart last changed"], DataFieldType.DateTime, row => row.Parent.ModifiedUtc, cart);

        AddField(nameof(CartLine.LineId), S["Line ID"], DataFieldType.Text, row => row.Child.LineId, line).Identifier();
        AddField("Position", S["Line number"], DataFieldType.Integer, row => row.Position, line);
        AddField("ProductId", S["Product ID"], DataFieldType.Text, row => row.Child.ContentItemId, line)
            .Identifier(CommerceReportDataSets.ProductReference());
        AddField(nameof(CartLine.VariantId), S["Variant ID"], DataFieldType.Text, row => row.Child.VariantId, line)
            .Identifier(CommerceReportDataSets.VariantReference());
        AddField(nameof(CartLine.PriceId), S["Price ID"], DataFieldType.Text, row => row.Child.PriceId, line);
        AddField(nameof(CartLine.Quantity), S["Quantity"], DataFieldType.Integer, row => row.Child.Quantity, line);
        AddField(nameof(CartLine.AddedUnitPrice), S["Unit price when added"], DataFieldType.Decimal, row => row.Child.AddedUnitPrice, line).Description = inCurrency;
        AddField("Value", S["Value when added"], DataFieldType.Decimal, row => row.Child.AddedUnitPrice * row.Child.Quantity, line).Description = inCurrency;
        AddField(AddedUtcField, S["Added"], DataFieldType.DateTime, row => row.Child.AddedUtc, line);

        CompleteDescription();

        // The main date is not in the index, so it is the default date without being pushed down.
        Descriptor.DefaultDateField = AddedUtcField;
    }

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<CartIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<CartIndex, string>>>(StringComparer.Ordinal)
        {
            [CartIdField] = index => index.ItemId,
            [nameof(Cart.UserId)] = index => index.UserId,
        };

    /// <inheritdoc/>
    protected override IEnumerable<ReportChildRow<Cart, CartLine>> GetRows(Cart document)
    {
        return ReportChildRow<Cart, CartLine>.For(document, document.Lines);
    }
}
