using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Assets.Core.Indexes;
using CrestApps.OrchardCore.Assets.Models;
using CrestApps.OrchardCore.Assets.Services;
using CrestApps.OrchardCore.YesSql.Core.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using YesSql;
using YesSql.Services;
using Asset =CrestApps.OrchardCore.Assets.Models.Asset;

namespace CrestApps.OrchardCore.Assets.Core.Services;

/// <summary>
/// Stores asset types.
/// </summary>
public sealed class AssetTypeStore : DocumentCatalog<AssetType, AssetTypeIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AssetTypeStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    public AssetTypeStore(ISession session)
        : base(session)
    {
        CollectionName = AssetsConstants.CollectionName;
    }

    /// <summary>
    /// Returns a page of the asset types, ordered by name, and how many there are, read in the database.
    /// </summary>
    /// <param name="skip">How many types to skip.</param>
    /// <param name="take">How many types to return.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>How many types there are, and the page of them.</returns>
    public Task<(int Count, IReadOnlyList<AssetType> Items)> PageAsync(int skip, int take, CancellationToken cancellationToken = default)
    {
        return PageByNameAsync(index => index.Name, null, skip, take, cancellationToken);
    }
}

/// <summary>
/// Manages asset types.
/// </summary>
public sealed class AssetTypeManager : CatalogManager<AssetType>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AssetTypeManager"/> class.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="handlers">The handlers.</param>
    /// <param name="logger">The logger.</param>
    public AssetTypeManager(AssetTypeStore store, IEnumerable<ICatalogEntryHandler<AssetType>> handlers, ILogger<CatalogManager<AssetType>> logger)
        : base(store, handlers, logger)
    {
    }
}

/// <summary>
/// Stores assets.
/// </summary>
public sealed class AssetStore : DocumentCatalog<Asset, AssetIndex>
{
    private readonly IEnumerable<IAssetUsage> _usages;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AssetStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="usages">The modules whose records point to assets.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AssetStore(ISession session, IEnumerable<IAssetUsage> usages, IStringLocalizer<AssetStore> stringLocalizer)
        : base(session)
    {
        _usages = usages;
        S = stringLocalizer;
        CollectionName = AssetsConstants.CollectionName;
    }

    /// <summary>
    /// Returns why records of other modules point to an asset.
    /// </summary>
    /// <param name="asset">The asset.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The reasons; empty when nothing points to the asset.</returns>
    public async Task<IReadOnlyList<string>> GetUsageReasonsAsync(Asset asset, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(asset);

        var reasons = new List<string>();

        foreach (var usage in _usages)
        {
            if (await usage.GetUsageAsync(asset, cancellationToken) is { Length: > 0 } reason)
            {
                reasons.Add(reason);
            }
        }

        return reasons;
    }

    /// <inheritdoc />
    protected override async ValueTask DeletingAsync(Asset model)
    {
        // Records that point to the asset would lose it: an asset in use is retired, never deleted. Checked here, in the
        // store, so no path deletes it; the admin page asks first and says why.
        var reasons = await GetUsageReasonsAsync(model);

        if (reasons.Count > 0)
        {
            throw new InvalidOperationException(S["The asset {0} is in use and can't be deleted: {1} Retire it instead.", model.Name, string.Join(" ", reasons)]);
        }
    }

    /// <summary>
    /// Lists the assets whose type is of a category, such as every vehicle, ordered by name, in the database.
    /// </summary>
    /// <param name="category">The category of their type.</param>
    /// <param name="statuses">The states to keep, or <see langword="null"/> or empty for every state.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The assets.</returns>
    public async Task<IReadOnlyList<Asset>> ListByCategoryAsync(AssetCategory category, IReadOnlyCollection<AssetStatus> statuses = null, CancellationToken cancellationToken = default)
    {
        var typeIds = await ListTypeIdsAsync(Session, category, cancellationToken);

        if (typeIds.Length == 0)
        {
            return [];
        }

        var query = Session.Query<Asset, AssetIndex>(index => index.AssetTypeId.IsIn(typeIds), collection: CollectionName);

        if (statuses is { Count: > 0 })
        {
            var values = statuses.Select(status => status.ToString()).ToArray();

            query = query.Where(index => index.Status.IsIn(values));
        }

        return await LoadedAsync(await query.OrderBy(index => index.Name).ListAsync(cancellationToken));
    }

    /// <summary>
    /// Lists the ids of the asset types of a category, in the database.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="category">The category.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The type ids.</returns>
    public static async Task<string[]> ListTypeIdsAsync(ISession session, AssetCategory category, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var value = category.ToString();

        return [.. (await session.QueryIndex<AssetTypeIndex>(index => index.Category == value, collection: AssetsConstants.CollectionName).ListAsync(cancellationToken))
            .Select(index => index.ItemId)];
    }

    /// <summary>
    /// Counts the assets of each type, in the database, without loading them.
    /// </summary>
    /// <param name="typeIds">The asset types.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of assets of each type.</returns>
    public async Task<IReadOnlyDictionary<string, int>> CountByTypeAsync(IEnumerable<string> typeIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(typeIds);

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var typeId in typeIds.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal))
        {
            counts[typeId] = await Session.QueryIndex<AssetIndex>(index => index.AssetTypeId == typeId, collection: CollectionName).CountAsync(cancellationToken);
        }

        return counts;
    }

    /// <summary>
    /// Finds the assets whose name, number or serial number contains a text, ordered by name, in the database. A blank
    /// text finds the first assets by name.
    /// </summary>
    /// <param name="text">The text, or <see langword="null"/> for the first assets.</param>
    /// <param name="take">The most assets to return.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The assets found.</returns>
    public async Task<IReadOnlyList<Asset>> SearchAsync(string text, int take, CancellationToken cancellationToken = default)
    {
        if (take <= 0)
        {
            return [];
        }

        var query = Session.Query<Asset, AssetIndex>(collection: CollectionName);

        if (!string.IsNullOrWhiteSpace(text))
        {
            var search = text.Trim();

            query = query.Where(index => index.Name.Contains(search) || index.Number.Contains(search) || index.SerialNumber.Contains(search));
        }

        return await LoadedAsync(await query.OrderBy(index => index.Name).Take(take).ListAsync(cancellationToken));
    }
}

/// <summary>
/// Manages assets.
/// </summary>
public sealed class AssetManager : CatalogManager<Asset>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AssetManager"/> class.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="handlers">The handlers.</param>
    /// <param name="logger">The logger.</param>
    public AssetManager(AssetStore store, IEnumerable<ICatalogEntryHandler<Asset>> handlers, ILogger<CatalogManager<Asset>> logger)
        : base(store, handlers, logger)
    {
    }
}

/// <summary>
/// Stores meter readings.
/// </summary>
public sealed class AssetMeterReadingStore : DocumentCatalog<AssetMeterReading, AssetMeterReadingIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AssetMeterReadingStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    public AssetMeterReadingStore(ISession session)
        : base(session)
    {
        CollectionName = AssetsConstants.CollectionName;
    }
}

/// <summary>
/// Checks and stamps asset types.
/// </summary>
internal sealed class AssetTypeHandler : CatalogEntryHandlerBase<AssetType>
{
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AssetTypeHandler"/> class.
    /// </summary>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AssetTypeHandler(IClock clock, IStringLocalizer<AssetTypeHandler> stringLocalizer)
    {
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task InitializedAsync(InitializedContext<AssetType> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task ValidatingAsync(ValidatingContext<AssetType> context, CancellationToken cancellationToken = default)
    {
        var type = context.Model;

        type.Attributes = [.. type.Attributes.Where(attribute => !string.IsNullOrWhiteSpace(attribute?.Name)).Select(attribute => { attribute.Name = attribute.Name.Trim(); return attribute; })];
        type.Meters = [.. type.Meters.Where(meter => !string.IsNullOrWhiteSpace(meter?.Name)).Select(meter => { meter.Name = meter.Name.Trim(); meter.Unit = meter.Unit?.Trim(); return meter; })];

        if (string.IsNullOrWhiteSpace(type.Name))
        {
            context.Result.Fail(new ValidationResult(S["The name is required."], [nameof(AssetType.Name)]));
        }

        if (type.Attributes.GroupBy(attribute => attribute.Name, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
        {
            context.Result.Fail(new ValidationResult(S["Two attributes have the same name."], [nameof(AssetType.Attributes)]));
        }

        if (type.Meters.GroupBy(meter => meter.Name, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
        {
            context.Result.Fail(new ValidationResult(S["Two meters have the same name."], [nameof(AssetType.Meters)]));
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Checks, numbers and stamps assets.
/// </summary>
internal sealed class AssetHandler : CatalogEntryHandlerBase<Asset>
{
    private readonly ISession _session;
    private readonly AssetTypeStore _typeStore;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AssetHandler"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="typeStore">The asset type store.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AssetHandler(ISession session, AssetTypeStore typeStore, IClock clock, IStringLocalizer<AssetHandler> stringLocalizer)
    {
        _session = session;
        _typeStore = typeStore;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task InitializedAsync(InitializedContext<Asset> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override async Task CreatingAsync(CreatingContext<Asset> context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Model.Number))
        {
            context.Model.Number = await AssetNumbers.NextAsync(_session, cancellationToken);
        }
    }

    /// <inheritdoc/>
    public override Task UpdatingAsync(UpdatingContext<Asset> context, CancellationToken cancellationToken = default)
    {
        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override async Task ValidatingAsync(ValidatingContext<Asset> context, CancellationToken cancellationToken = default)
    {
        var asset = context.Model;

        asset.Number = string.IsNullOrWhiteSpace(asset.Number) ? null : asset.Number.Trim();

        if (string.IsNullOrWhiteSpace(asset.Name))
        {
            context.Result.Fail(new ValidationResult(S["The name is required."], [nameof(Asset.Name)]));
        }

        if (string.IsNullOrEmpty(asset.AssetTypeId) || await _typeStore.FindByIdAsync(asset.AssetTypeId, cancellationToken) is null)
        {
            context.Result.Fail(new ValidationResult(S["Choose the asset's type."], [nameof(Asset.AssetTypeId)]));
        }

        if (asset.OwnerKind != AssetOwnerKind.Internal && string.IsNullOrWhiteSpace(asset.OwnerId))
        {
            context.Result.Fail(new ValidationResult(S["Choose who owns the asset."], [nameof(Asset.OwnerId)]));
        }

        if (asset.Number is not null)
        {
            var number = asset.Number;
            var taken = await _session.QueryIndex<AssetIndex>(index => index.Number == number && index.ItemId != asset.ItemId, collection: AssetsConstants.CollectionName).FirstOrDefaultAsync(cancellationToken);

            if (taken is not null)
            {
                context.Result.Fail(new ValidationResult(S["Another asset has the number {0}.", number], [nameof(Asset.Number)]));
            }
        }

        if (asset.Warranty?.StartDate > asset.Warranty?.EndDate)
        {
            context.Result.Fail(new ValidationResult(S["The warranty can't end before it starts."], [nameof(Asset.Warranty)]));
        }

        // An asset can't sit inside itself, directly or further up.
        var parentId = asset.ParentAssetId;
        var seen = new HashSet<string>(StringComparer.Ordinal);

        while (!string.IsNullOrEmpty(parentId) && seen.Add(parentId))
        {
            if (parentId == asset.ItemId)
            {
                context.Result.Fail(new ValidationResult(S["An asset can't be inside itself."], [nameof(Asset.ParentAssetId)]));

                break;
            }

            var parent = await _session.QueryIndex<AssetIndex>(index => index.ItemId == parentId, collection: AssetsConstants.CollectionName).FirstOrDefaultAsync(cancellationToken);

            if (parent is null && parentId == asset.ParentAssetId)
            {
                context.Result.Fail(new ValidationResult(S["The asset it is part of doesn't exist."], [nameof(Asset.ParentAssetId)]));
            }

            parentId = parent?.ParentAssetId;
        }
    }
}

/// <summary>
/// Gives assets their numbers.
/// </summary>
public static class AssetNumbers
{
    /// <summary>
    /// The prefix of asset numbers.
    /// </summary>
    public const string Prefix = "A-";

    /// <summary>
    /// Gives the next free number: one more than the highest A- number so far.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number.</returns>
    public static async Task<string> NextAsync(ISession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var numbers = await session.QueryIndex<AssetIndex>(index => index.Number.StartsWith(Prefix), collection: AssetsConstants.CollectionName).ListAsync(cancellationToken);
        var highest = numbers
            .Select(index => int.TryParse(index.Number.AsSpan(Prefix.Length), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : 0)
            .DefaultIfEmpty(0)
            .Max();

        return string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{Prefix}{highest + 1:00000}");
    }
}
