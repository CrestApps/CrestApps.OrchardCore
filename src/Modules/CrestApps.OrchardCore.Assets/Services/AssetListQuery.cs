using CrestApps.OrchardCore.Assets.Core.Indexes;
using CrestApps.OrchardCore.Assets.Models;
using YesSql;
using YesSql.Services;
using Asset = CrestApps.OrchardCore.Assets.Models.Asset;

namespace CrestApps.OrchardCore.Assets.Services;

/// <summary>
/// The query of the Assets list: type (or a category's types), status, owner and a search on name, number or serial number, used by the list
/// page and the <c>listAssets</c> AI tool.
/// </summary>
internal static class AssetListQuery
{
    /// <summary>
    /// Builds the query; the caller orders and pages it.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="typeId">The asset type, or <see langword="null"/>.</param>
    /// <param name="status">The status, or <see langword="null"/>.</param>
    /// <param name="ownerKind">The owner's kind; applied with <paramref name="ownerId"/> only.</param>
    /// <param name="ownerId">The owner's id; applied with <paramref name="ownerKind"/> only.</param>
    /// <param name="search">Text in the name, number or serial number, or <see langword="null"/>.</param>
    /// <param name="typeIds">Without <paramref name="typeId"/>, the asset types to keep, such as those of a category;
    /// <see langword="null"/> for all.</param>
    /// <returns>The query.</returns>
    public static IQuery<Asset, AssetIndex> Build(ISession session, string typeId, AssetStatus? status, AssetOwnerKind? ownerKind, string ownerId, string search, string[] typeIds = null)
    {
        ArgumentNullException.ThrowIfNull(session);

        var query = session.Query<Asset, AssetIndex>(collection: AssetsConstants.CollectionName);

        if (!string.IsNullOrEmpty(typeId))
        {
            query = query.Where(index => index.AssetTypeId == typeId);
        }
        else if (typeIds is not null)
        {
            query = query.Where(index => index.AssetTypeId.IsIn(typeIds));
        }

        if (status is { } statusValue)
        {
            var value = statusValue.ToString();

            query = query.Where(index => index.Status == value);
        }

        if (ownerKind is { } kindValue && !string.IsNullOrEmpty(ownerId))
        {
            var kind = kindValue.ToString();

            query = query.Where(index => index.OwnerKind == kind && index.OwnerId == ownerId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim();

            query = query.Where(index => index.Name.Contains(q) || index.Number.Contains(q) || index.SerialNumber.Contains(q));
        }

        return query;
    }
}
