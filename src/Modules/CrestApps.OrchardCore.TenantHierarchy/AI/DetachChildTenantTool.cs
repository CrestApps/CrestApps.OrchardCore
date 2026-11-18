using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.TenantHierarchy.AI;

/// <summary>
/// Makes an orphaned child tenant an ordinary tenant, as the Detach action of the orphans list does. The page offers
/// it for orphans only, so a child whose parent still exists is refused.
/// </summary>
internal sealed class DetachChildTenantTool : TenantHierarchyAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "detachChildTenant";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "childTenantName": {
          "type": "string",
          "description": "The tenant name of an orphaned child tenant, from the orphans getTenantHierarchyOverview returns."
        }
      },
      "required": ["childTenantName"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Makes an orphaned child tenant an ordinary tenant that no parent manages.";

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
        var platformService = services.GetRequiredService<TenantHierarchyPlatformService>();

        if (childName is null || !platformService.GetOverview().Orphans.Any(orphan => string.Equals(orphan.Settings.Name, childName, StringComparison.OrdinalIgnoreCase)))
        {
            return $"Nothing was changed: '{childName}' is not an orphaned child tenant. Only a child whose parent no longer exists can be detached; use moveChildTenant to give a child another parent.";
        }

        var result = await platformService.DetachChildAsync(childName);

        if (!result.Succeeded)
        {
            return $"Nothing was changed: {result.Error}";
        }

        return Json(new
        {
            childTenantName = childName,
            detached = true,
            message = "The tenant is no longer a child tenant. It keeps its host and its data.",
        });
    }
}
