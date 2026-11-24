using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Assets.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Assets.AI;

/// <summary>
/// One asset as its details page shows it: type and attributes, owner, location, status, warranty and whether it covers
/// today, the assets it sits in and those inside it, the latest meter readings and, with Fleet, its vehicle details.
/// </summary>
internal sealed class GetAssetTool : AssetToolBase
{
    public const string ToolName = "getAsset";

    private const int MaxReadings = 50;

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "asset": { "type": "string", "description": "The asset's ID or number (such as A-000123), from listAssets." }
      },
      "required": ["asset"],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Gets one asset: type and attributes, owner, location, status, warranty and whether it covers today, the assets it sits in and those inside it, its latest meter readings and, with Fleet, its vehicle details and calendar. Use it for \"is it still under warranty?\".";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => AssetsPermissions.ViewAssets;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var key = AssetTools.Read(arguments, "asset");

        if (key is null)
        {
            return "Give the asset's ID or number, from listAssets.";
        }

        var asset = await AssetTools.FindAsync(services, key, cancellationToken);

        if (asset is null)
        {
            return $"No asset has the ID or number '{key}'. Use listAssets to find it.";
        }

        var service = services.GetRequiredService<AssetService>();
        var localClock = services.GetRequiredService<ILocalClock>();
        var type = string.IsNullOrEmpty(asset.AssetTypeId) ? null : await services.GetRequiredService<AssetTypeManager>().FindByIdAsync(asset.AssetTypeId, cancellationToken);
        var today = await AssetTools.TodayAsync(services);
        var ancestors = await service.AncestorsAsync(asset, cancellationToken);
        var children = await service.ChildrenAsync(asset.ItemId, cancellationToken);
        var readings = await service.ReadingsAsync(asset.ItemId, MaxReadings, cancellationToken);
        var latest = new List<object>();

        foreach (var reading in readings.GroupBy(item => item.Meter, StringComparer.OrdinalIgnoreCase).Select(group => group.First()))
        {
            var meter = type?.Meters.FirstOrDefault(item => string.Equals(item.Name, reading.Meter, StringComparison.OrdinalIgnoreCase));

            latest.Add(new
            {
                meter = reading.Meter,
                value = reading.Value,
                unit = meter?.Unit,
                cumulative = meter?.Cumulative,
                at = await AssetTools.LocalAsync(localClock, reading.Utc),
                source = reading.Source.ToString(),
                recordedBy = reading.RecordedBy,
            });
        }

        var warranty = asset.Warranty;
        var fleet = asset.Fleet;

        return AssetTools.Json(new
        {
            id = asset.ItemId,
            number = asset.Number,
            name = asset.Name,
            type = type is null ? null : new { id = type.ItemId, name = type.Name, category = type.Category.ToString() },
            serialNumber = asset.SerialNumber,
            manufacturer = asset.Manufacturer,
            model = asset.Model,
            productId = asset.ProductId,
            status = asset.Status.ToString(),
            owner = await AssetTools.OwnerAsync(services.GetRequiredService<AssetOwnerDirectory>(), asset, cancellationToken),
            location = asset.Location is null ? null : new
            {
                addressLine1 = asset.Location.AddressLine1,
                city = asset.Location.City,
                region = asset.Location.Region,
                postalCode = asset.Location.PostalCode,
                country = asset.Location.Country,
            },
            installDate = AssetTools.Day(asset.InstallDate),
            warranty = warranty is null ? null : new
            {
                start = AssetTools.Day(warranty.StartDate),
                end = AssetTools.Day(warranty.EndDate),
                provider = warranty.Provider,
                terms = warranty.Terms,
                coversToday = warranty.Covers(today),
                daysLeft = warranty.Covers(today) && warranty.EndDate is { } end ? end.DayNumber - today.DayNumber : (int?)null,
            },
            today = AssetTools.Day(today),
            attributes = (type?.Attributes ?? [])
                .Select(attribute => new { name = attribute.Name, kind = attribute.Kind.ToString(), value = asset.Attributes?.GetValueOrDefault(attribute.Name) })
                .ToList(),
            documents = asset.Documents?.ToList() ?? [],
            insideOf = ancestors.Select(item => new { id = item.ItemId, number = item.Number, name = item.Name }).ToList(),
            contains = children.Select(item => new { id = item.ItemId, number = item.Number, name = item.Name, status = item.Status.ToString() }).ToList(),
            latestReadings = latest,
            fleet = fleet is null ? null : new
            {
                schedulable = fleet.Schedulable,
                calendarId = fleet.CalendarId,
                plate = fleet.Plate,
                vin = fleet.Vin,
                capacity = fleet.Capacity,
                fuelType = fleet.FuelType,
                homeBaseCity = fleet.HomeBase?.City,
                defaultDriverWorkerId = fleet.DefaultDriverWorkerId,
                carriesStock = fleet.CarriesStock,
                stockLocationId = fleet.StockLocationId,
            },
        });
    }
}
