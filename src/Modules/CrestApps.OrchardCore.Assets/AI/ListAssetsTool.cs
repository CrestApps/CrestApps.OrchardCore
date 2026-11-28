using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Assets.Core.Indexes;
using CrestApps.OrchardCore.Assets.Core.Services;
using CrestApps.OrchardCore.Assets.Models;
using CrestApps.OrchardCore.Assets.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Assets.AI;

/// <summary>
/// Lists assets with the query of the Assets page (type, status, owner, search), by name.
/// </summary>
internal sealed class ListAssetsTool : AssetToolBase
{
    public const string ToolName = "listAssets";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "search": { "type": "string", "description": "Optional. Text in the asset's name, number or serial number." },
        "type": { "type": "string", "description": "Optional. An asset type's name or ID." },
        "status": { "type": "string", "enum": ["Active", "InService", "OutOfService", "Retired"], "description": "Optional. The asset's status." },
        "ownerKind": { "type": "string", "enum": ["Customer", "Account", "Contact"], "description": "Optional. The kind of owner; give it with ownerId." },
        "ownerId": { "type": "string", "description": "Optional. The owner's ID (a customer's user ID, an account or a contact); give it with ownerKind." },
        "page": { "type": "integer", "minimum": 1, "description": "The page, from 1." },
        "pageSize": { "type": "integer", "minimum": 1, "maximum": 50, "description": "Assets per page, 20 by default, at most 50." }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Lists assets filtered by type, owner, status and a search on name, number or serial number, with paging, each with its owner, the asset it sits in and whether its warranty covers today.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => AssetsPermissions.ViewAssets;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        AssetStatus? status = null;
        AssetOwnerKind? ownerKind = null;

        if (AssetTools.Has(arguments, "status"))
        {
            if (!AssetTools.TryReadEnum<AssetStatus>(arguments, "status", out var parsedStatus))
            {
                return "The status must be Active, InService, OutOfService or Retired.";
            }

            status = parsedStatus;
        }

        var ownerId = AssetTools.Read(arguments, "ownerId");

        if (AssetTools.Has(arguments, "ownerKind"))
        {
            if (!AssetTools.TryReadEnum<AssetOwnerKind>(arguments, "ownerKind", out var parsedOwner) || parsedOwner == AssetOwnerKind.Internal)
            {
                return "The owner kind must be Customer, Account or Contact.";
            }

            ownerKind = parsedOwner;
        }

        if ((ownerKind is null) != (ownerId is null))
        {
            return "Give both ownerKind and ownerId, or neither.";
        }

        var types = await services.GetRequiredService<AssetTypeManager>().GetAllAsync(cancellationToken);
        var typeName = AssetTools.Read(arguments, "type");
        string typeId = null;

        if (typeName is not null)
        {
            var type = types.FirstOrDefault(item => item.ItemId == typeName)
                ?? types.FirstOrDefault(item => string.Equals(item.Name?.Trim(), typeName, StringComparison.OrdinalIgnoreCase));

            if (type is null)
            {
                return $"No asset type is called '{typeName}'. The types are: {string.Join(", ", types.Select(item => item.Name).Order(StringComparer.CurrentCultureIgnoreCase))}.";
            }

            typeId = type.ItemId;
        }

        var session = services.GetRequiredService<ISession>();
        var (page, pageSize) = AssetTools.ReadPaging(arguments);
        var query = AssetListQuery.Build(session, typeId, status, ownerKind, ownerId, AssetTools.Read(arguments, "search"));
        var total = await query.CountAsync(cancellationToken);
        var assets = await query.OrderBy(index => index.Name).Skip((page - 1) * pageSize).Take(pageSize).ListAsync(cancellationToken);
        var parentIds = assets.Select(asset => asset.ParentAssetId).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray();
        var parents = parentIds.Length == 0
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : (await session.QueryIndex<AssetIndex>(index => index.ItemId.IsIn(parentIds), collection: AssetsConstants.CollectionName).ListAsync(cancellationToken))
                .ToDictionary(index => index.ItemId, index => index.Name, StringComparer.Ordinal);
        var typeNames = types.ToDictionary(type => type.ItemId, type => type.Name, StringComparer.Ordinal);
        var ownerNames = await services.GetRequiredService<AssetOwnerDirectory>().GetNamesAsync(assets, cancellationToken);
        var today = await AssetTools.TodayAsync(services);
        var items = assets.Select(asset => (object)new
        {
            id = asset.ItemId,
            number = asset.Number,
            name = asset.Name,
            type = typeNames.GetValueOrDefault(asset.AssetTypeId ?? string.Empty),
            serialNumber = asset.SerialNumber,
            status = asset.Status.ToString(),
            ownerKind = asset.OwnerKind.ToString(),
            owner = asset.OwnerKind == AssetOwnerKind.Internal ? null : ownerNames.GetValueOrDefault(AssetOwnerDirectory.Key(asset.OwnerKind, asset.OwnerId ?? string.Empty)) ?? asset.OwnerId,
            insideOf = parents.GetValueOrDefault(asset.ParentAssetId ?? string.Empty),
            warrantyEnd = AssetTools.Day(asset.Warranty?.EndDate),
            warrantyCoversToday = asset.Warranty?.Covers(today) ?? false,
        }).ToList();

        return AssetTools.Json(new
        {
            items,
            total,
            page,
            pageSize,
            truncated = ((page - 1) * pageSize) + items.Count < total,
        });
    }
}
