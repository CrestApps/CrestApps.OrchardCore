using CrestApps.OrchardCore.Products.Core.Models;
using OrchardCore.ContentManagement.Records;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Products.Reports.DataSources;

/// <summary>
/// The product types a principal may read in reports, and which version of their products it reads: the latest one,
/// drafts included, of the types it may edit, and the published one of the types it may only view. Each product then
/// gives one row, whatever number of versions it has.
/// </summary>
public sealed class ProductTypeAccess
{
    private readonly IReadOnlyDictionary<string, ProductPartSettings> _settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProductTypeAccess"/> class.
    /// </summary>
    /// <param name="latestTypes">The product types whose latest version is read.</param>
    /// <param name="publishedTypes">The product types whose published version is read.</param>
    /// <param name="settings">The product settings of each readable type, by type name.</param>
    public ProductTypeAccess(
        IReadOnlyList<string> latestTypes,
        IReadOnlyList<string> publishedTypes,
        IReadOnlyDictionary<string, ProductPartSettings> settings)
    {
        LatestTypes = latestTypes ?? [];
        PublishedTypes = publishedTypes ?? [];
        _settings = settings ?? new Dictionary<string, ProductPartSettings>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Gets the access of a principal that may read no product.
    /// </summary>
    public static ProductTypeAccess None { get; } = new([], [], null);

    /// <summary>
    /// Gets the product types whose latest version is read, drafts included: the types the principal may edit.
    /// </summary>
    public IReadOnlyList<string> LatestTypes { get; }

    /// <summary>
    /// Gets the product types whose published version is read: the types the principal may view but not edit.
    /// </summary>
    public IReadOnlyList<string> PublishedTypes { get; }

    /// <summary>
    /// Gets every product type the principal may read.
    /// </summary>
    public IEnumerable<string> Types => LatestTypes.Concat(PublishedTypes);

    /// <summary>
    /// Gets a value indicating whether the principal may read no product.
    /// </summary>
    public bool IsEmpty => LatestTypes.Count == 0 && PublishedTypes.Count == 0;

    /// <summary>
    /// Gets the product settings of a readable product type.
    /// </summary>
    /// <param name="contentType">The name of the content type.</param>
    /// <returns>The settings, or <see langword="null"/> when the type has none or is not readable.</returns>
    public ProductPartSettings GetSettings(string contentType)
    {
        return contentType is not null && _settings.TryGetValue(contentType, out var settings) ? settings : null;
    }

    /// <summary>
    /// Narrows the content item index rows to the one version of each product the principal reads.
    /// </summary>
    /// <param name="rows">The index rows.</param>
    /// <returns>The narrowed index rows.</returns>
    public IQueryIndex<ContentItemIndex> Restrict(IQueryIndex<ContentItemIndex> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var latest = LatestTypes.ToArray();
        var published = PublishedTypes.ToArray();

        if (published.Length == 0)
        {
            return rows.Where(index => index.Latest && index.ContentType.IsIn(latest));
        }

        if (latest.Length == 0)
        {
            return rows.Where(index => index.Published && index.ContentType.IsIn(published));
        }

        return rows.Where(index => (index.Latest && index.ContentType.IsIn(latest)) || (index.Published && index.ContentType.IsIn(published)));
    }
}
