using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.TenantHierarchy.AI;

/// <summary>
/// Deletes the leftovers of a child tenant that cannot be used, as the Discard and Dismiss actions of the child tenants
/// page do: a failed setup is discarded with its tenant, database and registry entry (<c>CreateChildTenants</c>), and the
/// entry of a tenant the platform removed or moved is dismissed (<c>ManageChildTenants</c>).
/// </summary>
internal sealed class DiscardChildTenantTool : TenantHierarchyAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "discardChildTenant";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "entryId": {
          "type": "string",
          "description": "The entry id of a child tenant in the Failed or ChangedByPlatform state, from listChildTenants."
        }
      },
      "required": ["entryId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes the leftovers of a child tenant that cannot be used: discards a failed setup with its registry entry, or dismisses the entry of a tenant the platform removed or moved.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override TenantHierarchyToolScope Scope => TenantHierarchyToolScope.Parent;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var canDiscard = await IsAuthorizedAsync(services, user, TenantHierarchyPermissions.CreateChildTenants);
        var canDismiss = await IsAuthorizedAsync(services, user, TenantHierarchyPermissions.ManageChildTenants);

        if (!canDiscard && !canDismiss)
        {
            return NotAllowedMessage;
        }

        var entryId = ReadString(arguments, "entryId");
        var info = await FindChildAsync(services, entryId, includeChangedByPlatform: true);

        if (info is null)
        {
            return ChildNotFound(entryId);
        }

        var manager = services.GetRequiredService<ChildTenantManager>();

        if (info.Entry.Status == ChildTenantStatus.Failed)
        {
            if (!canDiscard)
            {
                return NotAllowedMessage;
            }

            var discarded = await manager.DiscardAsync(entryId);

            return discarded.Succeeded
                ? Json(new
                {
                    entryId,
                    displayName = info.Entry.DisplayName,
                    discarded = true,
                    message = $"The failed setup of {info.Entry.DisplayName} was discarded with its database and registry entry. This cannot be undone.",
                })
                : $"{info.Entry.DisplayName} was not discarded: {discarded.Error}";
        }

        if (info.State == ChildTenantRuntimeState.ChangedByPlatform)
        {
            if (!canDismiss)
            {
                return NotAllowedMessage;
            }

            var dismissed = await manager.DismissAsync(entryId);

            return dismissed.Succeeded
                ? Json(new
                {
                    entryId,
                    displayName = info.Entry.DisplayName,
                    dismissed = true,
                    message = $"The entry of {info.Entry.DisplayName}, which the platform removed or moved, was removed from the list with its access grants.",
                })
                : $"{info.Entry.DisplayName} was not dismissed: {dismissed.Error}";
        }

        return $"Nothing was done: {info.Entry.DisplayName} is neither a failed setup nor changed by the platform. To remove a working child tenant, suspend it and use removeChildTenant.";
    }
}
