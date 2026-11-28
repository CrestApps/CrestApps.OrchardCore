using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.TenantHierarchy.AI;

/// <summary>
/// Removes a suspended child tenant, as the Remove screen of the child tenants page does: the tenant's exact display
/// name confirms it, and the removal waits for the policy's grace period when there is one.
/// </summary>
internal sealed class RemoveChildTenantTool : TenantHierarchyAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "removeChildTenant";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "entryId": {
          "type": "string",
          "description": "The entry id of the child tenant, from listChildTenants. It must be suspended."
        },
        "confirmName": {
          "type": "string",
          "description": "The child tenant's display name, exactly as listChildTenants shows it, to confirm the removal."
        }
      },
      "required": ["entryId", "confirmName"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Removes a suspended child tenant and its database, at once or after the policy's grace period. Requires the tenant's exact display name as confirmation.";

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
        var info = await FindChildAsync(services, entryId);

        if (info is null)
        {
            return ChildNotFound(entryId);
        }

        // The confirmation is compared as the Remove screen compares it: trimmed, exact case.
        if (!string.Equals(ReadString(arguments, "confirmName"), info.Entry.DisplayName, StringComparison.Ordinal))
        {
            return $"Nothing was removed: confirmName must be the child tenant's display name exactly, '{info.Entry.DisplayName}'. Ask the person to confirm the name before you try again.";
        }

        var result = await services.GetRequiredService<ChildTenantManager>().RemoveAsync(entryId);

        if (!result.Succeeded)
        {
            return $"{info.Entry.DisplayName} was not removed: {result.Error}";
        }

        var remaining = await services.GetRequiredService<ITenantHierarchyBroker>().GetChildAsync(entryId);

        if (remaining?.Entry.Status == ChildTenantStatus.PendingRemoval)
        {
            return Json(new
            {
                entryId,
                displayName = info.Entry.DisplayName,
                removed = false,
                removalScheduled = true,
                removalDate = await ToLocalAsync(services, remaining.Entry.RetainUntilUtc),
                message = $"{info.Entry.DisplayName} will be removed with its data on the removal date. Until then restoreChildTenant cancels the removal. Its open sessions were ended.",
            });
        }

        return Json(new
        {
            entryId,
            displayName = info.Entry.DisplayName,
            removed = true,
            message = $"{info.Entry.DisplayName} and its database were removed. This cannot be undone.",
        });
    }
}
