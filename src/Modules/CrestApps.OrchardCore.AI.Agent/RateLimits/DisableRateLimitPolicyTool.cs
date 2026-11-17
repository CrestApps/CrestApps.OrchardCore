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
/// Disables rate-limit policies through the policy store and asks for the tenant to reload, as the Disable action and
/// the bulk action of the Rate Limits admin page do. A policy that protects the sign-in and account endpoints is not
/// disabled unless the call says so, so the person who approves sees that this protection goes.
/// </summary>
public sealed class DisableRateLimitPolicyTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "disableRateLimitPolicy";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "policyIds": {
          "type": "array",
          "minItems": 1,
          "maxItems": 20,
          "description": "The ids of the policies to disable, from listRateLimitPolicies.",
          "items": {
            "type": "string"
          }
        },
        "confirmRemovesSignInProtection": {
          "type": "boolean",
          "description": "True to confirm that the person knows a policy of this call protects the sign-in or account endpoints (listRateLimitPolicies marks it with protectsSignIn) and wants that protection off. The call is refused when it disables such a policy without this."
        }
      },
      "required": [
        "policyIds"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Disables one or more rate-limit policies, which removes that protection, and reloads the tenant. It refuses to disable a policy that protects the sign-in or account endpoints unless the call confirms it.";

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

        var idsError = RateLimitToolSupport.ReadPolicyIds(arguments, out var policyIds);

        if (idsError is not null)
        {
            return $"Nothing was disabled: {idsError}";
        }

        var store = services.GetRequiredService<IRateLimitPolicyStore>();
        var toDisable = new List<RateLimitPolicy>();
        var alreadyDisabled = new List<string>();
        var notFound = new List<string>();
        var protecting = new List<string>();

        foreach (var policyId in policyIds)
        {
            var policy = await store.FindByIdAsync(policyId, PolicyVersion.Current);

            if (policy is null)
            {
                notFound.Add(policyId);
            }
            else if (!policy.IsEnabled)
            {
                alreadyDisabled.Add(policy.Name);
            }
            else
            {
                toDisable.Add(policy);

                var protection = RateLimitToolSupport.GetSignInProtection(services, policy);

                if (protection is not null)
                {
                    protecting.Add($"'{policy.Name}' {protection}");
                }
            }
        }

        // Everything is checked before anything is changed.
        if (protecting.Count > 0 && !arguments.GetFirstValueOrDefault("confirmRemovesSignInProtection", false))
        {
            return $"Nothing was disabled: {string.Join("; ", protecting)}. Disabling it takes that protection against password guessing away; the built-in limits that modules register stay on. Tell the person, and call again with confirmRemovesSignInProtection set to true when they still want it.";
        }

        var disabled = new List<object>();

        foreach (var policy in toDisable)
        {
            if (await store.SetStatusAsync(policy.PolicyId, false))
            {
                disabled.Add(new
                {
                    id = policy.PolicyId,
                    name = policy.Name,
                });
            }
            else
            {
                notFound.Add(policy.PolicyId);
            }
        }

        if (disabled.Count == 0)
        {
            return AgentToolResults.Json(new
            {
                disabled,
                alreadyDisabled,
                notFound,
                note = "Nothing was disabled. Use listRateLimitPolicies to get the ids of the policies.",
            });
        }

        // As the admin page does: the policy stops applying when the tenant reloads, at the end of this request.
        services.GetRequiredService<IShellReleaseManager>().RequestRelease();

        return AgentToolResults.Json(new
        {
            disabled,
            alreadyDisabled = alreadyDisabled.Count > 0
                ? alreadyDisabled
                : null,
            notFound = notFound.Count > 0
                ? notFound
                : null,
            signInProtectionRemoved = protecting.Count > 0
                ? protecting
                : null,
            tenantReloads = true,
            note = "The tenant reloads at the end of this request and the policies stop applying from then on. Turn a policy on again with enableRateLimitPolicy.",
        });
    }
}
