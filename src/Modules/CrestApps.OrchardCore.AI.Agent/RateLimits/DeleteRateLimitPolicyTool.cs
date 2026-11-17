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
/// Deletes rate-limit policies through the policy store, and asks for the tenant to reload when an enabled policy
/// went, as the Delete action and the bulk action of the Rate Limits admin page do. An enabled policy that protects
/// the sign-in and account endpoints is not deleted unless the call says so, so the person who approves sees that
/// this protection goes.
/// </summary>
public sealed class DeleteRateLimitPolicyTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteRateLimitPolicy";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "policyIds": {
          "type": "array",
          "minItems": 1,
          "maxItems": 20,
          "description": "The ids of the policies to delete, from listRateLimitPolicies.",
          "items": {
            "type": "string"
          }
        },
        "confirmRemovesSignInProtection": {
          "type": "boolean",
          "description": "True to confirm that the person knows an enabled policy of this call protects the sign-in or account endpoints (listRateLimitPolicies marks it with protectsSignIn) and wants that protection gone. The call is refused when it deletes such a policy without this."
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
    public override string Description => "Deletes one or more rate-limit policies; this cannot be undone, and the tenant reloads when an enabled policy is deleted. It refuses to delete an enabled policy that protects the sign-in or account endpoints unless the call confirms it. Suggest disableRateLimitPolicy when the person only wants the policy off for now.";

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

        if (!await AIToolCallerAuthorization.IsAuthorizedAsync(services, RateLimitsPermissions.ManageRateLimits))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var idsError = RateLimitToolSupport.ReadPolicyIds(arguments, out var policyIds);

        if (idsError is not null)
        {
            return $"Nothing was deleted: {idsError}";
        }

        var store = services.GetRequiredService<IRateLimitPolicyStore>();
        var toDelete = new List<RateLimitPolicy>();
        var notFound = new List<string>();
        var protecting = new List<string>();

        foreach (var policyId in policyIds)
        {
            var policy = await store.FindByIdAsync(policyId, PolicyVersion.Current);

            if (policy is null)
            {
                notFound.Add(policyId);

                continue;
            }

            toDelete.Add(policy);

            // A disabled policy protects nothing, so only an enabled one is asked about.
            var protection = RateLimitToolSupport.GetSignInProtection(services, policy);

            if (protection is not null)
            {
                protecting.Add($"'{policy.Name}' {protection}");
            }
        }

        // Everything is checked before anything is deleted.
        if (protecting.Count > 0 && !arguments.GetFirstValueOrDefault("confirmRemovesSignInProtection", false))
        {
            return $"Nothing was deleted: {string.Join("; ", protecting)}. Deleting it takes that protection against password guessing away; the built-in limits that modules register stay on. Tell the person, and call again with confirmRemovesSignInProtection set to true when they still want it.";
        }

        var deleted = new List<object>();
        var reload = false;

        foreach (var policy in toDelete)
        {
            if (await store.DeleteAsync(policy))
            {
                deleted.Add(new
                {
                    id = policy.PolicyId,
                    name = policy.Name,
                    wasEnabled = policy.IsEnabled,
                });

                reload |= policy.IsEnabled;
            }
            else
            {
                notFound.Add(policy.PolicyId);
            }
        }

        // As the admin page does: an enabled policy stops applying when the tenant reloads.
        if (reload)
        {
            services.GetRequiredService<IShellReleaseManager>().RequestRelease();
        }

        return AgentToolResults.Json(new
        {
            deleted,
            notFound = notFound.Count > 0
                ? notFound
                : null,
            signInProtectionRemoved = protecting.Count > 0 && deleted.Count > 0
                ? protecting
                : null,
            tenantReloads = reload
                ? true
                : (bool?)null,
            note = deleted.Count == 0
                ? "Nothing was deleted. Use listRateLimitPolicies to get the ids of the policies."
                : "The deleted policies cannot be restored.",
        });
    }
}
