using CrestApps.OrchardCore.Assets.Core.Indexes;
using CrestApps.OrchardCore.Assets;
using CrestApps.OrchardCore.Inventory.Core;
using CrestApps.OrchardCore.Inventory.Core.Indexes;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using YesSql;

namespace CrestApps.OrchardCore.WorkOrders.Services;

/// <summary>
/// Gives the work order pages the records other features keep: assets, stock items and stock locations.
/// </summary>
public interface IWorkOrderLookup
{
    /// <summary>
    /// Gives the assets a work order can be for.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The options, by id.</returns>
    Task<IReadOnlyList<SelectListItem>> AssetsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SelectListItem>>([]);

    /// <summary>
    /// Gives the stock items a line can take parts from.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The options, by id.</returns>
    Task<IReadOnlyList<SelectListItem>> StockItemsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SelectListItem>>([]);

    /// <summary>
    /// Gives the stock locations parts can come from, such as a warehouse or a van.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The options, by id.</returns>
    Task<IReadOnlyList<SelectListItem>> LocationsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SelectListItem>>([]);

    /// <summary>
    /// Lists the skills a job can need.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The skills.</returns>
    Task<IReadOnlyList<SelectListItem>> SkillsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SelectListItem>>([]);

    /// <summary>
    /// Lists the certifications a job can need.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The certifications.</returns>
    Task<IReadOnlyList<SelectListItem>> CertificationsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SelectListItem>>([]);
}

internal sealed class SkillsWorkOrderLookup : IWorkOrderLookup
{
    private readonly Skills.Services.ISkillManager _skills;
    private readonly Skills.Services.ICertificationManager _certifications;

    public SkillsWorkOrderLookup(Skills.Services.ISkillManager skills, Skills.Services.ICertificationManager certifications)
    {
        _skills = skills;
        _certifications = certifications;
    }

    public async Task<IReadOnlyList<SelectListItem>> SkillsAsync(CancellationToken cancellationToken = default)
        => [.. (await _skills.GetEnabledAsync(cancellationToken)).OrderBy(skill => skill.Name, StringComparer.CurrentCultureIgnoreCase).Select(skill => new SelectListItem(skill.Name, skill.ItemId))];

    public async Task<IReadOnlyList<SelectListItem>> CertificationsAsync(CancellationToken cancellationToken = default)
        => [.. (await _certifications.GetEnabledAsync(cancellationToken)).OrderBy(certification => certification.Name, StringComparer.CurrentCultureIgnoreCase).Select(certification => new SelectListItem(certification.Name, certification.ItemId))];
}

/// <summary>
/// Lists the Inventory stock items and locations.
/// </summary>
internal sealed class InventoryWorkOrderLookup : IWorkOrderLookup
{
    private const int MaxItems = 500;

    private readonly ISession _session;
    private readonly IInventoryLocationService _locations;

    /// <summary>
    /// Initializes a new instance of the <see cref="InventoryWorkOrderLookup"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="locations">The location service.</param>
    public InventoryWorkOrderLookup(ISession session, IInventoryLocationService locations)
    {
        _session = session;
        _locations = locations;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SelectListItem>> StockItemsAsync(CancellationToken cancellationToken = default)
    {
        // The index keeps the title and SKU normalized for search; the item has them as written.
        var items = await _session.Query<StockItem, StockItemIndex>(index => !index.Archived, collection: InventoryConstants.CollectionName)
            .OrderBy(index => index.NormalizedTitle)
            .Take(MaxItems)
            .ListAsync(cancellationToken);

        return [.. items.Select(item => new SelectListItem(string.IsNullOrEmpty(item.Sku) ? item.Title : $"{item.Title} ({item.Sku})", item.ItemId))];
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SelectListItem>> LocationsAsync(CancellationToken cancellationToken = default)
        => [.. (await _locations.GetLocationsAsync()).Select(location => new SelectListItem(location.Name, location.LocationId))];
}

/// <summary>
/// Lists the assets.
/// </summary>
internal sealed class AssetsWorkOrderLookup : IWorkOrderLookup
{
    private const int MaxItems = 500;

    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="AssetsWorkOrderLookup"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    public AssetsWorkOrderLookup(ISession session)
    {
        _session = session;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SelectListItem>> AssetsAsync(CancellationToken cancellationToken = default)
    {
        var assets = await _session.QueryIndex<AssetIndex>(collection: AssetsConstants.CollectionName)
            .OrderBy(index => index.Name)
            .Take(MaxItems)
            .ListAsync(cancellationToken);

        return [.. assets.Select(asset => new SelectListItem($"{asset.Name} ({asset.Number})", asset.ItemId))];
    }
}

/// <summary>
/// Puts the lookups of every feature that is on together.
/// </summary>
public sealed class WorkOrderLookups
{
    private readonly IEnumerable<IWorkOrderLookup> _lookups;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkOrderLookups"/> class.
    /// </summary>
    /// <param name="lookups">The lookups.</param>
    public WorkOrderLookups(IEnumerable<IWorkOrderLookup> lookups)
    {
        _lookups = lookups;
    }

    /// <summary>
    /// Gives the assets.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The options.</returns>
    public Task<List<SelectListItem>> AssetsAsync(CancellationToken cancellationToken = default) => AllAsync(lookup => lookup.AssetsAsync(cancellationToken));

    /// <summary>
    /// Gives the stock items.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The options.</returns>
    public Task<List<SelectListItem>> StockItemsAsync(CancellationToken cancellationToken = default) => AllAsync(lookup => lookup.StockItemsAsync(cancellationToken));

    /// <summary>
    /// Gives the stock locations.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The options.</returns>
    public Task<List<SelectListItem>> LocationsAsync(CancellationToken cancellationToken = default) => AllAsync(lookup => lookup.LocationsAsync(cancellationToken));

    /// <summary>
    /// Lists the skills a job can need.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The skills.</returns>
    public Task<List<SelectListItem>> SkillsAsync(CancellationToken cancellationToken = default) => AllAsync(lookup => lookup.SkillsAsync(cancellationToken));

    /// <summary>
    /// Lists the certifications a job can need.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The certifications.</returns>
    public Task<List<SelectListItem>> CertificationsAsync(CancellationToken cancellationToken = default) => AllAsync(lookup => lookup.CertificationsAsync(cancellationToken));

    private async Task<List<SelectListItem>> AllAsync(Func<IWorkOrderLookup, Task<IReadOnlyList<SelectListItem>>> get)
    {
        var options = new List<SelectListItem>();

        foreach (var lookup in _lookups)
        {
            options.AddRange(await get(lookup));
        }

        return options;
    }
}
