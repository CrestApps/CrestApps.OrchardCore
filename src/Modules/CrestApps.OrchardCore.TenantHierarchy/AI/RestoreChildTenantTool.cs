using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.TenantHierarchy.AI;

/// <summary>
/// Cancels the pending removal of a child tenant, as the Restore action of the child tenants page does.
/// </summary>
internal sealed class RestoreChildTenantTool : TenantHierarchyAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "restoreChildTenant";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "entryId": {
          "type": "string",
          "description": "The entry id of a child tenant pending removal, from listChildTenants."
        }
      },
      "required": ["entryId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Cancels the pending removal of a child tenant during its grace period. The tenant stays suspended until resumed.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override TenantHierarchyToolScope Scope => TenantHierarchyToolScope.Parent;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedAsync(services, user, TenantHierarchyPermissions.RemoveChildTenants))
        {
            return NotAllowedMessage;
        }

        var entryId = ReadString(arguments, "entryId");
        var info = await FindChildAsync(services, entryId, includeChangedByPlatform: true);

        if (info is null)
        {
            return ChildNotFound(entryId);
        }

        var result = await services.GetRequiredService<ChildTenantManager>().RestoreAsync(entryId);

        if (!result.Succeeded)
        {
            return $"{info.Entry.DisplayName} was not restored: only a child tenant pending removal can be restored.";
        }

        return Json(new
        {
            entryId,
            displayName = info.Entry.DisplayName,
            restored = true,
            message = $"{info.Entry.DisplayName} was restored. It stays suspended until you resume it with changeChildTenantState.",
        });
    }
}
