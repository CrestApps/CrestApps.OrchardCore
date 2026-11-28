using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell;
using OrchardCore.RateLimits.Core;
using OrchardCore.RateLimits.Models;

namespace CrestApps.OrchardCore.AI.Agent.RateLimits;

/// <summary>
/// Enables or disables rate-limit policies through the policy store and asks for the tenant to reload, as the Enable
/// and Disable actions and the bulk actions of the Rate Limits admin page do. A policy that protects the sign-in and
/// account endpoints is not disabled unless the call says so, so the person who approves sees that this protection
/// goes.
/// </summary>
internal sealed class SetRateLimitPolicyEnabledTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "setRateLimitPolicyEnabled";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "policyIds": {
          "type": "array",
          "minItems": 1,
          "maxItems": 20,
          "description": "The ids of the policies to enable or disable, from listRateLimitPolicies.",
          "items": {
            "type": "string"
          }
        },
        "enabled": {
          "type": "boolean",
          "description": "True to enable the policies, false to disable them."
        },
        "confirmRemovesSignInProtection": {
          "type": "boolean",
          "description": "Only when disabling: true to confirm that the person knows a policy of this call protects the sign-in or account endpoints (listRateLimitPolicies marks it with protectsSignIn) and wants that protection off. A call that disables such a policy without this is refused."
        }
      },
      "required": [
        "policyIds",
        "enabled"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Enables or disables one or more rate-limit policies, and the tenant reloads. When enabling, tell the person the limits of each policy (from listRateLimitPolicies) first: a global policy with a low limit locks everyone out, this chat included. Disabling removes that protection, and is refused for a policy that protects the sign-in or account endpoints unless the call confirms it.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var services = arguments.Services;

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, RateLimitsPermissions.ManageRateLimits))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!arguments.TryGetValue("enabled", out var enabledValue) || enabledValue is null)
        {
            return "Nothing was changed: give enabled, true to enable the policies or false to disable them.";
        }

        var enable = arguments.GetFirstValueOrDefault("enabled", false);
        var idsError = RateLimitToolSupport.ReadPolicyIds(arguments, out var policyIds);

        if (idsError is not null)
        {
            return $"Nothing was changed: {idsError}";
        }

        var store = services.GetRequiredService<IRateLimitPolicyStore>();
        var toChange = new List<RateLimitPolicy>();
        var alreadySet = new List<string>();
        var notFound = new List<string>();
        var protecting = new List<string>();

        foreach (var policyId in policyIds)
        {
            var policy = await store.FindByIdAsync(policyId, PolicyVersion.Current);

            if (policy is null)
            {
                notFound.Add(policyId);
            }
            else if (policy.IsEnabled == enable)
            {
                alreadySet.Add(policy.Name);
            }
            else
            {
                toChange.Add(policy);

                if (!enable)
                {
                    var protection = RateLimitToolSupport.GetSignInProtection(services, policy);

                    if (protection is not null)
                    {
                        protecting.Add($"'{policy.Name}' {protection}");
                    }
                }
            }
        }

        // Everything is checked before anything is changed.
        if (protecting.Count > 0 && !arguments.GetFirstValueOrDefault("confirmRemovesSignInProtection", false))
        {
            return $"Nothing was changed: {string.Join("; ", protecting)}. Disabling it takes that protection against password guessing away; the built-in limits that modules register stay on. Tell the person, and call again with confirmRemovesSignInProtection set to true when they still want it.";
        }

        var changed = new List<object>();

        foreach (var policy in toChange)
        {
            if (!await store.SetStatusAsync(policy.PolicyId, enable))
            {
                notFound.Add(policy.PolicyId);

                continue;
            }

            changed.Add(enable
                ? await RateLimitToolSupport.DescribeAsync(services, await store.FindByIdAsync(policy.PolicyId, PolicyVersion.Current) ?? policy)
                : new
                {
                    id = policy.PolicyId,
                    name = policy.Name,
                });
        }

        if (changed.Count == 0)
        {
            return AgentToolResults.Json(new
            {
                enabled = enable,
                changed,
                alreadySet,
                notFound,
                note = enable
                    ? "Nothing was enabled. Use listRateLimitPolicies to get the ids of the policies."
                    : "Nothing was disabled. Use listRateLimitPolicies to get the ids of the policies.",
            });
        }

        // As the admin page does: a policy starts or stops applying when the tenant reloads, at the end of this request.
        services.GetRequiredService<IShellReleaseManager>().RequestRelease();

        return AgentToolResults.Json(new
        {
            enabled = enable,
            changed,
            alreadySet = alreadySet.Count > 0
                ? alreadySet
                : null,
            notFound = notFound.Count > 0
                ? notFound
                : null,
            signInProtectionRemoved = protecting.Count > 0
                ? protecting
                : null,
            tenantReloads = true,
            note = enable
                ? "The tenant reloads at the end of this request and the policies apply from then on. Requests over a limit get a 429 response. A policy without limiters limits nothing."
                : "The tenant reloads at the end of this request and the policies stop applying from then on. Turn a policy on again with setRateLimitPolicyEnabled and enabled true.",
        });
    }
}
