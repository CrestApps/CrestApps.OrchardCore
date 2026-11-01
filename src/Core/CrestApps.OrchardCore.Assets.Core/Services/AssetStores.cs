using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Assets.Core.Indexes;
using CrestApps.OrchardCore.Assets.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using YesSql;
using Asset = CrestApps.OrchardCore.Assets.Models.Asset;

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
    /// <summary>
    /// Initializes a new instance of the <see cref="AssetStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    public AssetStore(ISession session)
        : base(session)
    {
        CollectionName = AssetsConstants.CollectionName;
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
            context.Result.Fail(new ValidationResult(S["Say which customer or company owns the asset."], [nameof(Asset.OwnerId)]));
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
