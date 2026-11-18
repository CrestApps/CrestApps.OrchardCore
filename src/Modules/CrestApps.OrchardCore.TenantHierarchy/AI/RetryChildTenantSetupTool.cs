using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.TenantHierarchy.AI;

/// <summary>
/// Creates a child tenant whose setup failed again and queues its setup, as the Retry action of the child tenants page
/// does. The failed tenant and its database are removed first, and the new one gets a new entry id.
/// </summary>
internal sealed class RetryChildTenantSetupTool : TenantHierarchyAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "retryChildTenantSetup";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "entryId": {
          "type": "string",
          "description": "The entry id of a child tenant in the Failed state, from listChildTenants."
        }
      },
      "required": ["entryId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates a child tenant whose setup failed again, with the same name, address and recipe, and runs the setup. Use it only on entries in the Failed state.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override TenantHierarchyToolScope Scope => TenantHierarchyToolScope.Parent;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedAsync(services, user, TenantHierarchyPermissions.CreateChildTenants))
        {
            return NotAllowedMessage;
        }

        var entryId = ReadString(arguments, "entryId");
        var info = await FindChildAsync(services, entryId, includeChangedByPlatform: true);

        if (info is null)
        {
            return ChildNotFound(entryId);
        }

        if (info.Entry.Status != ChildTenantStatus.Failed)
        {
            return $"Nothing was done: {info.Entry.DisplayName} is not in the Failed state, so its setup cannot be retried.";
        }

        // Retrying removes the failed tenant and its database before it creates the new one, so nothing is done when
        // the new setup could not be queued.
        if (!ChildTenantSetupQueue.CanQueue)
        {
            return $"Nothing was done: the setup could not be queued here. A person can retry it on the child tenants page: {GetAdminPath(services, "TenantHierarchyChildren")}";
        }

        var (result, entry) = await services.GetRequiredService<ChildTenantManager>().RetryAsync(entryId);

        if (!result.Succeeded)
        {
            return $"The setup of {info.Entry.DisplayName} was not retried: {result.Error}";
        }

        ChildTenantSetupQueue.Queue(entry.EntryId);

        return Json(new
        {
            previousEntryId = entryId,
            entryId = entry.EntryId,
            displayName = entry.DisplayName,
            setupQueued = true,
            message = $"{entry.DisplayName} was created again with the entry id {entry.EntryId}, and its setup runs in the background after this reply. Call listChildTenants to follow it.",
        });
    }
}
