using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.TenantHierarchy.AI;

/// <summary>
/// Enables or disables features in a running child tenant, as the Features screen of the child tenants page does. Only
/// the features that screen offers can be changed, so a feature the parent's policy blocks never gets through.
/// </summary>
internal sealed class SetChildTenantFeaturesTool : TenantHierarchyAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "setChildTenantFeatures";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "entryId": {
          "type": "string",
          "description": "The entry id of a running child tenant, from listChildTenants."
        },
        "featureIds": {
          "type": "array",
          "items": { "type": "string" },
          "minItems": 1,
          "description": "The ids of the features to change, from listChildTenantFeatures."
        },
        "enable": {
          "type": "boolean",
          "description": "true to enable the features, false to disable them."
        }
      },
      "required": ["entryId", "featureIds", "enable"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Enables or disables features in a child tenant. Blocked and always-on features are ignored; the tenant must be running.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override TenantHierarchyToolScope Scope => TenantHierarchyToolScope.Parent;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedAsync(services, user, TenantHierarchyPermissions.ManageChildFeatures))
        {
            return NotAllowedMessage;
        }

        var enable = ReadBool(arguments, "enable");

        if (enable is null)
        {
            return "Nothing was changed: set enable to true or false.";
        }

        var featureIds = (ReadStringList(arguments, "featureIds") ?? []).Distinct(StringComparer.Ordinal).ToList();

        if (featureIds.Count == 0)
        {
            return "Nothing was changed: give the ids of the features in featureIds. Call listChildTenantFeatures to find them.";
        }

        var entryId = ReadString(arguments, "entryId");
        var info = await FindChildAsync(services, entryId);

        if (info is null)
        {
            return ChildNotFound(entryId);
        }

        if (info.State != ChildTenantRuntimeState.Running)
        {
            return $"Nothing was changed: {info.Entry.DisplayName} is not running ({DescribeState(info)}). Its features can only be changed while it runs.";
        }

        var offered = (await services.GetRequiredService<ITenantHierarchyBroker>().GetChildFeaturesAsync(entryId))
            .ToDictionary(feature => feature.Id, StringComparer.Ordinal);
        var accepted = new List<string>();
        var ignored = new List<object>();

        foreach (var featureId in featureIds)
        {
            if (!offered.TryGetValue(featureId, out var feature))
            {
                ignored.Add(new { id = featureId, reason = "This child tenant may not change this feature: it is blocked by the policy, is not installed, or is not offered on the Features screen." });
            }
            else if (!enable.Value && feature.IsAlwaysEnabled)
            {
                ignored.Add(new { id = featureId, reason = "This feature is always on." });
            }
            else if (feature.IsEnabled == enable.Value)
            {
                ignored.Add(new { id = featureId, reason = enable.Value ? "Already on." : "Already off." });
            }
            else
            {
                accepted.Add(featureId);
            }
        }

        if (accepted.Count == 0)
        {
            return Json(new
            {
                entryId,
                changed = false,
                ignored,
                message = "None of the features can be changed.",
            });
        }

        var result = await services.GetRequiredService<ChildTenantManager>().SetFeaturesAsync(entryId, accepted, enable.Value);

        if (!result.Succeeded)
        {
            return $"The features of {info.Entry.DisplayName} were not changed: {result.Error}";
        }

        return Json(new
        {
            entryId,
            displayName = info.Entry.DisplayName,
            changed = true,
            enabled = enable.Value ? accepted : null,
            disabled = enable.Value ? null : accepted,
            ignored,
            message = enable.Value
                ? "The features were enabled, with the features they depend on."
                : "The features were disabled, with the features that depend on them.",
        });
    }
}
