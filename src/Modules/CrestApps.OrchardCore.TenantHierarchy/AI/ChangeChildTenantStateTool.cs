using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.TenantHierarchy.AI;

/// <summary>
/// Suspends, resumes or reloads child tenants, as the row and bulk actions of the child tenants page do.
/// </summary>
internal sealed class ChangeChildTenantStateTool : TenantHierarchyAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "changeChildTenantState";

    private const int MaxEntries = 50;

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "entryIds": {
          "type": "array",
          "items": { "type": "string" },
          "minItems": 1,
          "maxItems": 50,
          "description": "The entry ids of the child tenants, from listChildTenants."
        },
        "action": {
          "type": "string",
          "enum": ["Suspend", "Resume", "Reload"],
          "description": "Suspend takes a running child tenant offline, Resume brings a suspended one back, Reload restarts one."
        }
      },
      "required": ["entryIds", "action"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Suspends, resumes or reloads one or more child tenants. Suspending takes the site offline for its users; a tenant pending removal must be restored before it can be resumed.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override TenantHierarchyToolScope Scope => TenantHierarchyToolScope.Parent;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedAsync(services, user, TenantHierarchyPermissions.ManageChildTenants))
        {
            return NotAllowedMessage;
        }

        var action = ReadString(arguments, "action");
        var entryIds = (ReadStringList(arguments, "entryIds") ?? []).Distinct(StringComparer.Ordinal).ToList();

        if (entryIds.Count == 0)
        {
            return "Nothing was changed: give the entry ids of the child tenants in entryIds. Call listChildTenants to find them.";
        }

        if (entryIds.Count > MaxEntries)
        {
            return $"Nothing was changed: give at most {MaxEntries} entry ids at a time.";
        }

        var manager = services.GetRequiredService<ChildTenantManager>();
        Func<string, Task<TenantHierarchyResult>> operation = action?.ToLowerInvariant() switch
        {
            "suspend" => manager.SuspendAsync,
            "resume" => manager.ResumeAsync,
            "reload" => manager.ReloadAsync,
            _ => null,
        };

        if (operation is null)
        {
            return "Nothing was changed: the action must be Suspend, Resume or Reload.";
        }

        var results = new List<object>();
        var changedCount = 0;

        foreach (var entryId in entryIds)
        {
            // The row actions refuse an entry that does not exist; an entry the platform changed is reported by the manager.
            var info = await FindChildAsync(services, entryId, includeChangedByPlatform: true);

            if (info is null)
            {
                results.Add(new { entryId, changed = false, error = ChildNotFound(entryId) });

                continue;
            }

            var result = await operation(entryId);

            if (result.Succeeded)
            {
                changedCount++;
            }

            results.Add(new
            {
                entryId,
                displayName = info.Entry.DisplayName,
                changed = result.Succeeded,
                error = result.Succeeded ? null : result.Error,
            });
        }

        return Json(new
        {
            action = char.ToUpperInvariant(action[0]) + action[1..].ToLowerInvariant(),
            changedCount,
            results,
        });
    }
}
