using System.Text.Json;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell;
using OrchardCore.RateLimits.Core;
using OrchardCore.RateLimits.Models;

namespace CrestApps.OrchardCore.AI.Agent.RateLimits;

/// <summary>
/// Enables rate-limit policies through the policy store and asks for the tenant to reload, as the Enable action and
/// the bulk action of the Rate Limits admin page do.
/// </summary>
public sealed class EnableRateLimitPolicyTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "enableRateLimitPolicy";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "policyIds": {
          "type": "array",
          "minItems": 1,
          "maxItems": 20,
          "description": "The ids of the policies to enable, from listRateLimitPolicies.",
          "items": {
            "type": "string"
          }
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
    public override string Description => "Enables one or more rate-limit policies. Requests over the limit then get 429 responses, and the tenant reloads. Tell the person the limits of each policy (from listRateLimitPolicies) first: a global policy with a low limit locks everyone out, this chat included.";

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
            return $"Nothing was enabled: {idsError}";
        }

        var store = services.GetRequiredService<IRateLimitPolicyStore>();
        var enabled = new List<object>();
        var alreadyEnabled = new List<string>();
        var notFound = new List<string>();

        foreach (var policyId in policyIds)
        {
            var policy = await store.FindByIdAsync(policyId, PolicyVersion.Current);

            if (policy is null)
            {
                notFound.Add(policyId);

                continue;
            }

            if (policy.IsEnabled)
            {
                alreadyEnabled.Add(policy.Name);

                continue;
            }

            if (!await store.SetStatusAsync(policyId, true))
            {
                notFound.Add(policyId);

                continue;
            }

            enabled.Add(await RateLimitToolSupport.DescribeAsync(services, await store.FindByIdAsync(policyId, PolicyVersion.Current) ?? policy));
        }

        if (enabled.Count == 0)
        {
            return AgentToolResults.Json(new
            {
                enabled,
                alreadyEnabled,
                notFound,
                note = "Nothing was enabled. Use listRateLimitPolicies to get the ids of the policies.",
            });
        }

        // As the admin page does: a policy takes effect when the tenant reloads, at the end of this request.
        services.GetRequiredService<IShellReleaseManager>().RequestRelease();

        return AgentToolResults.Json(new
        {
            enabled,
            alreadyEnabled = alreadyEnabled.Count > 0
                ? alreadyEnabled
                : null,
            notFound = notFound.Count > 0
                ? notFound
                : null,
            tenantReloads = true,
            note = "The tenant reloads at the end of this request and the policies apply from then on. Requests over a limit get a 429 response. A policy without limiters limits nothing.",
        });
    }
}
