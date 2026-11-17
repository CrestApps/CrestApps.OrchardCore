using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.RateLimits;
using OrchardCore.RateLimits.Core;
using OrchardCore.RateLimits.Models;

namespace CrestApps.OrchardCore.AI.Agent.RateLimits;

/// <summary>
/// Lists the rate-limit policies through the policy store, as the Rate Limits admin page does: every policy, filtered
/// by text and status, ordered by name and paged, with the built-in limits that modules register in code.
/// </summary>
public sealed class ListRateLimitPoliciesTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listRateLimitPolicies";

    // What a built-in limit that is not bound to an HTTP method applies to.
    private static readonly IReadOnlyList<string> _anyMethod = ["any"];

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "search": {
          "type": "string",
          "description": "Text the name, description, path or group of a policy must contain. Leave it out to list every policy."
        },
        "status": {
          "type": "string",
          "enum": [
            "enabled",
            "disabled"
          ],
          "description": "List the enabled policies only, or the disabled ones only. Leave it out for both."
        },
        "page": {
          "type": "integer",
          "minimum": 1,
          "description": "The page to return, starting at 1. The default is 1."
        },
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "The number of policies on a page. The default is 20 and the most is 50."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the site's rate-limit policies with scope, path or group, status and limiters, plus the built-in route and group limits that modules register. Optional search text and status filter. Use it to get the id of a policy and to tell the person the limits before a policy is enabled.";

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

        bool? enabled = null;

        if (arguments.TryGetFirstString("status", out var status))
        {
            switch (status.Trim().ToLowerInvariant())
            {
                case "enabled":
                    enabled = true;
                    break;
                case "disabled":
                    enabled = false;
                    break;
                default:
                    return "Nothing was listed: status must be enabled or disabled.";
            }
        }

        arguments.TryGetFirstString("search", out var search);

        search = search?.Trim();

        var (page, pageSize) = AgentToolResults.ReadPaging(arguments);
        var policies = (await services.GetRequiredService<IRateLimitPolicyStore>().GetAllAsync(PolicyVersion.Current))
            .Where(policy => enabled is null || policy.IsEnabled == enabled)
            .Where(policy => string.IsNullOrEmpty(search) || Matches(policy, search))
            .OrderBy(policy => policy.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var items = new List<object>();

        foreach (var policy in policies.Skip((page - 1) * pageSize).Take(pageSize))
        {
            items.Add(await RateLimitToolSupport.DescribeAsync(services, policy));
        }

        var options = services.GetService<IOptions<RateLimitsOptions>>()?.Value;

        return AgentToolResults.Json(new
        {
            items,
            total = policies.Count,
            page,
            pageSize,
            truncated = policies.Count > page * pageSize
                ? true
                : (bool?)null,

            // The limits modules register in code. They are read-only and stay on whatever happens to the policies.
            builtInRouteLimits = options?.RouteRateLimits
                .OrderBy(limit => limit.RouteName, StringComparer.OrdinalIgnoreCase)
                .Select(limit => new
                {
                    routeName = limit.RouteName,
                    methods = limit.HttpMethods.Count == 0
                        ? _anyMethod
                        : limit.HttpMethods,
                })
                .ToList(),
            builtInGroupLimits = options?.GroupRateLimits
                .GroupBy(limit => limit.GroupName, StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => new
                {
                    group = group.Key,
                    limiterCount = group.Count(),
                })
                .ToList(),
            note = "The built-in limits cannot be changed here. A policy applies only while it is enabled.",
        });
    }

    private static bool Matches(RateLimitPolicy policy, string search)
    {
        return Contains(policy.Name, search) ||
            Contains(policy.Description, search) ||
            Contains(policy.Path, search) ||
            Contains(policy.GroupName, search);
    }

    private static bool Contains(string value, string search)
        => !string.IsNullOrWhiteSpace(value) && value.Contains(search, StringComparison.OrdinalIgnoreCase);
}
