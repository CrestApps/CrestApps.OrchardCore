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
/// One row per signed-in shopper's wishlist: whose it is, how many products it holds, and when it last changed.
/// </summary>
/// <remarks>
/// The wishlists index has no date, so a date range on <see cref="ModifiedUtcField"/> is applied to the rows read
/// rather than to the index: wishlists are read newest first by their index rows.
/// </remarks>
public sealed class WishlistsReportDataSet : IndexedReportDataSet<Wishlist, WishlistIndex>, ICartReportDataSet
{
    /// <summary>
    /// The name of the field that holds when the wishlist last changed, the data set's main date.
    /// </summary>
    public const string ModifiedUtcField = "ModifiedUtc";

    /// <summary>
    /// Initializes a new instance of the <see cref="WishlistsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session wishlists are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WishlistsReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<WishlistsReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.CartSource.Wishlists,
                stringLocalizer["Wishlists"],
                stringLocalizer["One row per signed-in shopper's wishlist: whose it is, how many products it holds, and when it last changed."]),
            session,
            authorizationService,
            CartConstants.CollectionName,
            CartReportPermissions.ManageOrders)
    {
        var S = stringLocalizer;
        var wishlist = S["Wishlist"].Value;

        AddField(CommerceReportDataSets.ItemIdField, S["Wishlist ID"], DataFieldType.Text, record => record.ItemId, wishlist).Identifier();
        AddField(nameof(Wishlist.UserId), S["User ID"], DataFieldType.Text, record => record.UserId, wishlist).Identifier(ReportDataReferences.User());
        AddField("ItemCount", S["Products saved"], DataFieldType.Integer, record => record.Items?.Count ?? 0, wishlist);
        AddField(ModifiedUtcField, S["Last changed"], DataFieldType.DateTime, record => record.ModifiedUtc, wishlist);

        CompleteDescription();

        // The index has no date, so the main date is the default date without being pushed down.
        Descriptor.DefaultDateField = ModifiedUtcField;
    }

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<WishlistIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<WishlistIndex, string>>>(StringComparer.Ordinal)
        {
            [CommerceReportDataSets.ItemIdField] = index => index.ItemId,
            [nameof(Wishlist.UserId)] = index => index.UserId,
        };
}
