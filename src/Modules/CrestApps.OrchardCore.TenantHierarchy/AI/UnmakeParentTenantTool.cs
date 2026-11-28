using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.TenantHierarchy.AI;

/// <summary>
/// Turns a parent tenant without children back into an ordinary tenant, as the Unmake action of a parent's page does.
/// </summary>
internal sealed class UnmakeParentTenantTool : TenantHierarchyAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "unmakeParentTenant";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "tenantName": {
          "type": "string",
          "description": "The tenant name of the parent tenant, from getTenantHierarchyOverview. It must have no child tenants left."
        }
      },
      "required": ["tenantName"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Turns a parent tenant back into an ordinary tenant. Only works when it has no children left.";

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
        var result = await services.GetRequiredService<TenantHierarchyPlatformService>().UnmakeParentAsync(tenantName);

        if (!result.Succeeded)
        {
            return $"Nothing was changed: {result.Error}";
        }

        return Json(new
        {
            tenantName,
            unmade = true,
            message = "The tenant is no longer a parent tenant. It keeps its host; its hierarchy settings and policy were cleared.",
        });
    }
}
