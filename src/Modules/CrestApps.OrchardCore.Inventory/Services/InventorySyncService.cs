using CrestApps.OrchardCore.Inventory.Core;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Settings;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Inventory.Services;

/// <summary>
/// Brings stock items up to date with products: one product after it is saved, or every product whose type counts
/// stock, after the Inventory part is added to a type or the default low-stock threshold changes.
/// </summary>
public sealed class InventorySyncService
{
    private const int PageSize = 100;

    private readonly IContentManager _contentManager;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly InventoryProductSynchronizer _synchronizer;
    private readonly ISiteService _siteService;
    private readonly ISession _session;

    public InventorySyncService(
        IContentManager contentManager,
        IContentDefinitionManager contentDefinitionManager,
        InventoryProductSynchronizer synchronizer,
        ISiteService siteService,
        ISession session)
    {
        _contentManager = contentManager;
        _contentDefinitionManager = contentDefinitionManager;
        _synchronizer = synchronizer;
        _siteService = siteService;
        _session = session;
    }

    /// <summary>
    /// Syncs the products with these ids against their latest versions; a product that is gone has its stock items
    /// archived.
    /// </summary>
    public async Task SyncAsync(IEnumerable<string> contentItemIds, CancellationToken cancellationToken = default)
    {
        var settings = await _siteService.GetSettingsAsync<InventorySettings>();
        var tracked = await GetTrackedTypesAsync();

        foreach (var contentItemId in contentItemIds)
        {
            // The latest version, so stock can be counted before a product is published.
            var product = await _contentManager.GetAsync(contentItemId, VersionOptions.Latest);

            await _synchronizer.SyncAsync(contentItemId, product, product is not null && tracked.Contains(product.ContentType), settings, cancellationToken);
        }
    }

    /// <summary>
    /// Syncs every product whose content type has the Inventory part, and returns how many.
    /// </summary>
    public async Task<int> SyncAllAsync(CancellationToken cancellationToken = default)
    {
        var types = (await GetTrackedTypesAsync()).ToArray();

        if (types.Length == 0)
        {
            return 0;
        }

        var count = 0;

        for (var skip = 0; ; skip += PageSize)
        {
            var ids = (await _session.QueryIndex<ContentItemIndex>(index => index.Latest && index.ContentType.IsIn(types))
                .OrderBy(index => index.Id)
                .Skip(skip)
                .Take(PageSize)
                .ListAsync(cancellationToken))
                .Select(index => index.ContentItemId)
                .ToArray();

            await SyncAsync(ids, cancellationToken);
            count += ids.Length;

            if (ids.Length < PageSize)
            {
                return count;
            }
        }
    }

    private async Task<HashSet<string>> GetTrackedTypesAsync()
        => (await _contentDefinitionManager.ListTypeDefinitionsAsync())
            .Where(type => type.Parts.Any(part => part.PartDefinition.Name == InventoryConstants.Parts.InventoryPart))
            .Select(type => type.Name)
            .ToHashSet(StringComparer.Ordinal);
}
