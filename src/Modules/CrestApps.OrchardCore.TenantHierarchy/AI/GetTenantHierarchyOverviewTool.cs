using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.TenantHierarchy.Controllers;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.TenantHierarchy.AI;

/// <summary>
/// Shows the tenant hierarchy as the Tenant Hierarchy page of the Default tenant does, or one parent as its page does.
/// </summary>
internal sealed class GetTenantHierarchyOverviewTool : TenantHierarchyAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "getTenantHierarchyOverview";

    private const int MaxItems = 50;

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "parent": {
          "type": "string",
          "description": "The tenant name of a parent tenant, to get its policy, its children and the problems found. Leave it out for the whole hierarchy."
        },
        "search": {
          "type": "string",
          "description": "Text to find in the name or address of a parent or of one of its children, for the whole hierarchy."
        }
      },
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Shows the tenant hierarchy from the Default tenant: parent tenants with their children, orphaned children, tenants that can become parents, the database pools and whether the host guard is installed. Pass a parent to get its policy and children.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override TenantHierarchyToolScope Scope => TenantHierarchyToolScope.Platform;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedAsync(services, user, TenantHierarchyPermissions.ManageTenantHierarchy))
        {
            return NotAllowedMessage;
        }

        var platformService = services.GetRequiredService<TenantHierarchyPlatformService>();
        var parentName = ReadString(arguments, "parent");

        if (parentName is not null)
        {
            var node = await platformService.GetParentDetailAsync(parentName);

            if (node is null)
            {
                return $"No parent tenant is named '{parentName}'. Call getTenantHierarchyOverview without a parent to list them.";
            }

            return Json(new
            {
                parent = TenantHierarchyPlatformToolResults.DescribeNode(node),
                policy = TenantHierarchyPlatformToolResults.DescribePolicy(node.Settings.GetParentPolicy()),
                children = node.Children.Take(MaxItems).Select(TenantHierarchyPlatformToolResults.DescribeNode).ToList(),
                childCount = node.Children.Count,
                truncated = node.Children.Count > MaxItems,
                parentPath = GetAdminPath(services, "TenantHierarchyPlatformParent", new { id = node.Settings.Name }),
            });
        }

        var overview = platformService.GetOverview();
        var search = ReadString(arguments, "search");
        var parents = overview.Parents.Where(parent => PlatformController.Matches(parent, search)).ToList();

        return Json(new
        {
            parents = parents.Take(MaxItems).Select(parent => new
            {
                tenant = TenantHierarchyPlatformToolResults.DescribeNode(parent),
                childCount = parent.Children.Count,
                maxChildren = parent.Settings.GetParentPolicy().MaxChildren,
            }).ToList(),
            parentCount = parents.Count,
            orphans = overview.Orphans.Take(MaxItems).Select(TenantHierarchyPlatformToolResults.DescribeNode).ToList(),
            orphanCount = overview.Orphans.Count,
            candidates = overview.Candidates.Take(MaxItems).Select(TenantHierarchyPlatformToolResults.DescribeCandidate).ToList(),
            candidateCount = overview.Candidates.Count,
            truncated = parents.Count > MaxItems || overview.Orphans.Count > MaxItems || overview.Candidates.Count > MaxItems,
            databasePools = platformService.GetDatabasePoolNames(),
            hostGuardInstalled = platformService.IsHostGuardInstalled,
            platformDomain = platformService.GetPlatformDomain(),
            overviewPath = GetAdminPath(services, "TenantHierarchyPlatform"),
        });
    }
}
