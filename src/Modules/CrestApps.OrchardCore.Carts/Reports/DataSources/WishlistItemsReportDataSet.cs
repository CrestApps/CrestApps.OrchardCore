using System.Linq.Expressions;
using CrestApps.OrchardCore.Carts.Core;
using CrestApps.OrchardCore.Carts.Wishlists;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Carts.Reports.DataSources;

/// <summary>
/// One row per product saved to a wishlist, newest first in each wishlist: the product, when it was saved, and the
/// shopper who saved it.
/// </summary>
public sealed class WishlistItemsReportDataSet : IndexedReportDataSet<Wishlist, WishlistIndex, ReportChildRow<Wishlist, WishlistItem>>, ICartReportDataSet
{
    /// <summary>
    /// The name of the field that holds the identifier of the wishlist a product is saved to.
    /// </summary>
    public const string WishlistIdField = "WishlistId";

    /// <summary>
    /// The name of the field that holds when the product was saved.
    /// </summary>
    public const string AddedUtcField = "AddedUtc";

    /// <summary>
    /// Initializes a new instance of the <see cref="WishlistItemsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session wishlists are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WishlistItemsReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<WishlistItemsReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.CartSource.WishlistItems,
                stringLocalizer["Wishlist items"],
                stringLocalizer["One row per product saved to a wishlist: the product, when it was saved, and the shopper who saved it."]),
            session,
            authorizationService,
            CartConstants.CollectionName,
            CartReportPermissions.ManageOrders)
    {
        var S = stringLocalizer;
        var item = S["Saved product"].Value;

        AddField(WishlistIdField, S["Wishlist ID"], DataFieldType.Text, row => row.Parent.ItemId, item)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.CartSource.Name, CommerceReportDataSets.CartSource.Wishlists));
        AddField(nameof(Wishlist.UserId), S["User ID"], DataFieldType.Text, row => row.Parent.UserId, item).Identifier(ReportDataReferences.User());
        AddField("Position", S["Position"], DataFieldType.Integer, row => row.Position, item);
        AddField("ProductId", S["Product ID"], DataFieldType.Text, row => row.Child.ContentItemId, item)
            .Identifier(CommerceReportDataSets.ProductReference());
        AddField(AddedUtcField, S["Saved"], DataFieldType.DateTime, row => row.Child.AddedUtc, item);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<WishlistIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<WishlistIndex, string>>>(StringComparer.Ordinal)
        {
            [WishlistIdField] = index => index.ItemId,
            [nameof(Wishlist.UserId)] = index => index.UserId,
        };

    /// <inheritdoc/>
    protected override IEnumerable<ReportChildRow<Wishlist, WishlistItem>> GetRows(Wishlist document)
    {
        return ReportChildRow<Wishlist, WishlistItem>.For(document, document.Items);
    }
}
