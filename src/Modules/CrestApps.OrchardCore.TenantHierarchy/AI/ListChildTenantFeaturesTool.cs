using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.TenantHierarchy.AI;

/// <summary>
/// Lists the features a running child tenant may enable, as the Features screen of the child tenants page does. Blocked
/// features, themes, features only the Default tenant has and features enabled only as a dependency are not offered.
/// </summary>
internal sealed class ListChildTenantFeaturesTool : TenantHierarchyAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listChildTenantFeatures";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "entryId": {
          "type": "string",
          "description": "The entry id of a running child tenant, from listChildTenants."
        },
        "search": {
          "type": "string",
          "description": "Text to find in the id, name, category or description of a feature."
        },
        "enabledOnly": {
          "type": "boolean",
          "description": "Only the features that are on."
        },
        "page": {
          "type": "integer",
          "minimum": 1,
          "description": "The page to return, from 1."
        },
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "The number of features per page, at most 50. Defaults to 20."
        }
      },
      "required": ["entryId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the features a running child tenant may enable, by category, with whether each is on.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override TenantHierarchyToolScope Scope => TenantHierarchyToolScope.Parent;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedAsync(services, user, TenantHierarchyPermissions.ManageChildFeatures))
        {
            return NotAllowedMessage;
        }

        var entryId = ReadString(arguments, "entryId");
        var info = await FindChildAsync(services, entryId);

        if (info is null)
        {
            return ChildNotFound(entryId);
        }

        if (info.State != ChildTenantRuntimeState.Running)
        {
            return $"{info.Entry.DisplayName} is not running ({DescribeState(info)}), so its features cannot be listed or changed. Resume it first.";
        }

        var features = await services.GetRequiredService<ITenantHierarchyBroker>().GetChildFeaturesAsync(entryId);
        var search = ReadString(arguments, "search");
        IEnumerable<ChildFeatureInfo> matching = features;

        if (search is not null)
        {
            matching = matching.Where(feature =>
                feature.Id.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (feature.Name?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (feature.Category?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (feature.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (ReadBool(arguments, "enabledOnly") == true)
        {
            matching = matching.Where(feature => feature.IsEnabled);
        }

        var filtered = matching.ToList();
        var (page, pageSize) = ReadPaging(arguments);

        return Json(new
        {
            entryId,
            displayName = info.Entry.DisplayName,
            items = filtered.Skip((page - 1) * pageSize).Take(pageSize).Select(feature => new
            {
                id = feature.Id,
                name = feature.Name,
                category = feature.Category,
                description = feature.Description,
                enabled = feature.IsEnabled,
                alwaysEnabled = feature.IsAlwaysEnabled,
                dependencies = feature.Dependencies,
            }).ToList(),
            total = filtered.Count,
            page,
            pageSize,
            truncated = filtered.Count > page * pageSize,
            enabledCount = features.Count(feature => feature.IsEnabled),
            featuresPath = GetAdminPath(services, "TenantHierarchyChildrenFeatures", new { id = entryId }),
        });
    }
}
