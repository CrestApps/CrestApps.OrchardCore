using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.TenantHierarchy.AI;

/// <summary>
/// Moves a child tenant to another parent, or gives an orphaned child a parent, as the Move and Adopt actions of the
/// Tenant Hierarchy pages do.
/// </summary>
internal sealed class MoveChildTenantTool : TenantHierarchyAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "moveChildTenant";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "childTenantName": {
          "type": "string",
          "description": "The tenant name of the child tenant, from getTenantHierarchyOverview."
        },
        "newParentTenantName": {
          "type": "string",
          "description": "The tenant name of the parent tenant it moves to, from getTenantHierarchyOverview."
        }
      },
      "required": ["childTenantName", "newParentTenantName"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Moves a child tenant to another parent, or gives an orphaned child a parent. Its address follows the new parent's pattern and sessions from the old parent end.";

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

        var childName = ReadString(arguments, "childTenantName");
        var newParentName = ReadString(arguments, "newParentTenantName");
        var result = await services.GetRequiredService<TenantHierarchyPlatformService>().MoveChildAsync(childName, newParentName);

        if (!result.Succeeded)
        {
            return $"Nothing was moved: {result.Error} Call getTenantHierarchyOverview to find the tenant names.";
        }

        services.GetRequiredService<IShellHost>().TryGetSettings(childName, out var child);

        return Json(new
        {
            childTenantName = childName,
            newParentTenantName = newParentName,
            moved = true,
            host = child?.GetPrimaryHost(),
            message = "The child tenant was moved. Its address follows the new parent's pattern, and sessions opened from the old parent end at their next check.",
        });
    }
}
