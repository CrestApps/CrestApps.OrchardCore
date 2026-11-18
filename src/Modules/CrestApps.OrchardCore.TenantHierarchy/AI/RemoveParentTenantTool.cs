using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.TenantHierarchy.AI;

/// <summary>
/// Removes a suspended parent tenant and all its child tenants with their data, as the Remove action of a parent's page
/// does: the parent's exact tenant name confirms it.
/// </summary>
internal sealed class RemoveParentTenantTool : TenantHierarchyAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "removeParentTenant";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "tenantName": {
          "type": "string",
          "description": "The tenant name of the parent tenant, from getTenantHierarchyOverview. It must be suspended."
        },
        "confirmName": {
          "type": "string",
          "description": "The parent's tenant name again, exactly, to confirm the removal."
        }
      },
      "required": ["tenantName", "confirmName"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Removes a suspended parent tenant and all its child tenants with their data. Requires the parent's exact tenant name as confirmation.";

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

        var tenantName = ReadString(arguments, "tenantName");

        // The confirmation is compared as the parent's page compares it: trimmed, exact case.
        if (tenantName is null || !string.Equals(ReadString(arguments, "confirmName"), tenantName, StringComparison.Ordinal))
        {
            return "Nothing was removed: confirmName must be the parent's tenant name exactly, as getTenantHierarchyOverview shows it. Ask the person to confirm the name before you try again.";
        }

        var result = await services.GetRequiredService<TenantHierarchyPlatformService>().RemoveParentAsync(tenantName);

        if (!result.Succeeded)
        {
            return $"Nothing more was removed: {result.Error}";
        }

        return Json(new
        {
            tenantName,
            removed = true,
            message = "The parent tenant and all its child tenants were removed with their data. This cannot be undone.",
        });
    }
}
