using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Assets.Core.Services;
using CrestApps.OrchardCore.Assets.Models;
using CrestApps.OrchardCore.Calendars;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using YesSql;

namespace CrestApps.OrchardCore.Assets.AI;

/// <summary>
/// Records a reading of one of an asset's meters, as the asset details page's Record a reading form does.
/// </summary>
internal sealed class RecordAssetMeterReadingTool : AssetToolBase
{
    public const string ToolName = "recordAssetMeterReading";

    private static readonly string[] _dateTimeFormats = ["yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd HH:mm"];

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "asset": { "type": "string", "description": "The asset's ID or number (such as A-000123), from listAssets." },
        "meter": { "type": "string", "description": "The meter's name, as the asset's type defines it (such as Hours or Odometer); getAsset lists them." },
        "value": { "type": "number", "description": "The reading. A cumulative meter cannot go below its last reading." },
        "at": { "type": "string", "description": "Optional. When it was read (yyyy-MM-ddTHH:mm) in the user's time zone; now when left out." }
      },
      "required": ["asset", "meter", "value"],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Records a reading of one of an asset's meters (such as hours or kilometres), now or at a given time. A cumulative meter cannot go down.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => AssetsPermissions.ManageAssets;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var key = AssetTools.Read(arguments, "asset");
        var meter = AssetTools.Read(arguments, "meter");
        var value = AssetTools.ReadDecimal(arguments, "value");

        if (key is null || meter is null || value is null)
        {
            return "Give the asset, the meter and the reading (a number).";
        }

        var asset = await AssetTools.FindAsync(services, key, cancellationToken);

        if (asset is null)
        {
            return $"No asset has the ID or number '{key}'. Use listAssets to find it.";
        }

        var localClock = services.GetRequiredService<ILocalClock>();
        DateTime utc = default;
        var at = AssetTools.Read(arguments, "at");

        if (at is not null)
        {
            if (!DateTime.TryParseExact(at, _dateTimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
            {
                return "Give the time of the reading as yyyy-MM-ddTHH:mm.";
            }

            utc = await localClock.ToUtcLenientAsync(local);

            if (utc > services.GetRequiredService<IClock>().UtcNow)
            {
                return "A reading can't be in the future.";
            }
        }

        var reading = new AssetMeterReading
        {
            AssetId = asset.ItemId,
            Meter = meter,
            Value = value.Value,
            Utc = utc,
            Source = MeterReadingSource.Manual,
            RecordedBy = user.Identity?.Name,
        };
        var errors = await services.GetRequiredService<AssetService>().RecordReadingAsync(reading, cancellationToken);

        if (errors.Count > 0)
        {
            return "Nothing was recorded: " + string.Join(" ", errors);
        }

        await services.GetRequiredService<ISession>().SaveChangesAsync(cancellationToken);

        return AssetTools.Json(new
        {
            recorded = true,
            readingId = reading.ItemId,
            asset = new { id = asset.ItemId, number = asset.Number, name = asset.Name },
            meter = reading.Meter,
            value = reading.Value,
            at = await AssetTools.LocalAsync(localClock, reading.Utc),
        });
    }
}
