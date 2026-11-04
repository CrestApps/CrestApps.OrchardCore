using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Geo.Core.Indexes;
using CrestApps.OrchardCore.Geo.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Geo.Core.Services;

/// <summary>
/// Stores service territories.
/// </summary>
public sealed class ServiceTerritoryStore : DocumentCatalog<ServiceTerritory, ServiceTerritoryIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceTerritoryStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    public ServiceTerritoryStore(ISession session)
        : base(session)
    {
        CollectionName = GeoConstants.CollectionName;
    }
}

/// <summary>
/// Manages service territories.
/// </summary>
public sealed class ServiceTerritoryManager : CatalogManager<ServiceTerritory>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceTerritoryManager"/> class.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="handlers">The handlers.</param>
    /// <param name="logger">The logger.</param>
    public ServiceTerritoryManager(ServiceTerritoryStore store, IEnumerable<ICatalogEntryHandler<ServiceTerritory>> handlers, ILogger<CatalogManager<ServiceTerritory>> logger)
        : base(store, handlers, logger)
    {
    }
}

/// <summary>
/// Stores geocoding requests.
/// </summary>
public sealed class GeocodeRequestStore : DocumentCatalog<GeocodeRequest, GeocodeRequestIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GeocodeRequestStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    public GeocodeRequestStore(ISession session)
        : base(session)
    {
        CollectionName = GeoConstants.CollectionName;
    }
}

/// <summary>
/// Validates, tidies and stamps service territories.
/// </summary>
internal sealed class ServiceTerritoryHandler : CatalogEntryHandlerBase<ServiceTerritory>
{
    private readonly ServiceTerritoryStore _store;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceTerritoryHandler"/> class.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ServiceTerritoryHandler(ServiceTerritoryStore store, IClock clock, IStringLocalizer<ServiceTerritoryHandler> stringLocalizer)
    {
        _store = store;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task InitializedAsync(InitializedContext<ServiceTerritory> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task UpdatingAsync(UpdatingContext<ServiceTerritory> context, CancellationToken cancellationToken = default)
    {
        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override async Task ValidatingAsync(ValidatingContext<ServiceTerritory> context, CancellationToken cancellationToken = default)
    {
        var territory = context.Model;

        territory.PostalCodes = [.. territory.PostalCodes.Select(TerritoryMatcher.NormalizePostalCode).Where(code => code.Length > 0).Distinct(StringComparer.Ordinal)];
        territory.Shape = string.IsNullOrWhiteSpace(territory.Shape) ? null : territory.Shape.Trim();

        if (string.IsNullOrWhiteSpace(territory.Name))
        {
            context.Result.Fail(new ValidationResult(S["The name is required."], [nameof(ServiceTerritory.Name)]));
        }

        if (territory.Shape is null && territory.PostalCodes.Count == 0)
        {
            context.Result.Fail(new ValidationResult(S["Draw the territory on the map, list its postal codes, or both."], [nameof(ServiceTerritory.Shape)]));
        }

        if (territory.Shape is not null && !GeoShape.TryParse(territory.Shape, out _, out var error))
        {
            context.Result.Fail(new ValidationResult(S["The shape can't be read: {0}", error], [nameof(ServiceTerritory.Shape)]));
        }

        if (territory.TravelSpeedKmh is <= 0 or > 200)
        {
            context.Result.Fail(new ValidationResult(S["The travel speed must be more than 0 and at most 200 km/h."], [nameof(ServiceTerritory.TravelSpeedKmh)]));
        }

        if (!string.IsNullOrEmpty(territory.ParentId))
        {
            // A territory can't sit inside itself, directly or further up.
            var all = (await _store.GetAllAsync(cancellationToken)).ToDictionary(item => item.ItemId, StringComparer.Ordinal);
            var parentId = territory.ParentId;
            var seen = new HashSet<string>(StringComparer.Ordinal);

            while (!string.IsNullOrEmpty(parentId) && seen.Add(parentId))
            {
                if (parentId == territory.ItemId)
                {
                    context.Result.Fail(new ValidationResult(S["A territory can't be inside itself."], [nameof(ServiceTerritory.ParentId)]));

                    break;
                }

                parentId = all.TryGetValue(parentId, out var parent) ? parent.ParentId : null;
            }
        }
    }
}
