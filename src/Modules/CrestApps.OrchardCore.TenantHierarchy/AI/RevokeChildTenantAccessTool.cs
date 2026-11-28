using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.TenantHierarchy.Core.Services;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.TenantHierarchy.AI;

/// <summary>
/// Removes an access grant, as the Remove action of the Access screens does.
/// </summary>
internal sealed class RevokeChildTenantAccessTool : TenantHierarchyAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "revokeChildTenantAccess";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "grantId": {
          "type": "string",
          "description": "The id of the access grant, from listChildTenantAccessGrants."
        }
      },
      "required": ["grantId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Removes an access grant by its id. Open sessions end at their next validation.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override TenantHierarchyToolScope Scope => TenantHierarchyToolScope.Parent;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedAsync(services, user, TenantHierarchyPermissions.ManageChildAccess))
        {
            return NotAllowedMessage;
        }

        var grantId = ReadString(arguments, "grantId");
        var grant = string.IsNullOrEmpty(grantId)
            ? null
            : await services.GetRequiredService<AccessGrantStore>().FindAsync(grantId);

        if (grant is null)
        {
            return $"No access grant has the id '{grantId}'. Call listChildTenantAccessGrants to find it.";
        }

        var described = await ListChildTenantAccessGrantsTool.DescribeGrantAsync(services, grant);
        var result = await services.GetRequiredService<AccessGrantManager>().RemoveAsync(grantId);

        if (!result.Succeeded)
        {
            return $"The access grant was not removed: {result.Error}";
        }

        return Json(new
        {
            revoked = true,
            grant = described,
            message = "Access was removed. Open sessions end at their next check.",
        });
    }
}
