using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.RateLimiting;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore;
using OrchardCore.Entities;
using OrchardCore.RateLimits.Core;
using OrchardCore.RateLimits.Models;

namespace CrestApps.OrchardCore.AI.Agent.RateLimits;

/// <summary>
/// Creates or changes a rate-limit policy through the policy store the Rate Limits admin page saves to, with the
/// checks of that page (a name no other policy has, a path that starts with a slash for an endpoint policy, a group
/// for a group policy, and no change of the target or the limiters while the policy is enabled) and the limits its
/// limiter editors set on the numbers. A new policy is always saved disabled, so the person can review its limits
/// before a second, approved call turns it on.
/// </summary>
public sealed class SaveRateLimitPolicyTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveRateLimitPolicy";

    // The most limiters one policy gets from this tool.
    private const int MaxLimiters = 10;

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "policyId": {
          "type": "string",
          "description": "The id of the policy to update, from listRateLimitPolicies. Leave it out to create a policy."
        },
        "name": {
          "type": "string",
          "description": "The name of the policy, which no other policy may have. It is required for a new policy."
        },
        "description": {
          "type": "string",
          "description": "What the policy is for. Leave it out to keep the description of a policy that is updated."
        },
        "scope": {
          "type": "string",
          "enum": [
            "global",
            "endpoint",
            "group"
          ],
          "description": "What the policy limits: global is every request of the site, endpoint is the requests whose path starts with path, group is the endpoints in a rate-limit group. It is required for a new policy and cannot be changed while the policy is enabled."
        },
        "path": {
          "type": "string",
          "description": "For an endpoint policy: the start of the request path, beginning with a slash, for example /api/contact."
        },
        "group": {
          "type": "string",
          "description": "For a group policy: the name of the rate-limit group."
        },
        "limiters": {
          "type": "array",
          "maxItems": 10,
          "description": "The complete list of limiters of the policy; it replaces the stored list and cannot be changed while the policy is enabled. Leave it out to keep the stored limiters. Each limiter counts requests for each client (signed-in user, or IP address).",
          "items": {
            "type": "object",
            "properties": {
              "type": {
                "type": "string",
                "enum": [
                  "fixedWindow",
                  "slidingWindow",
                  "concurrency",
                  "tokenBucket"
                ],
                "description": "fixedWindow allows permitLimit requests in each window of windowSeconds; slidingWindow does the same over a window that moves in segmentsPerWindow steps; concurrency allows permitLimit requests at the same time; tokenBucket holds up to tokenLimit tokens, one for each request, and adds tokensPerPeriod every replenishmentPeriodSeconds."
              },
              "permitLimit": {
                "type": "integer",
                "minimum": 1,
                "description": "For fixedWindow, slidingWindow and concurrency: the number of requests allowed."
              },
              "windowSeconds": {
                "type": "integer",
                "minimum": 1,
                "description": "For fixedWindow and slidingWindow: the length of the window in seconds."
              },
              "segmentsPerWindow": {
                "type": "integer",
                "minimum": 1,
                "description": "For slidingWindow: the number of segments the window is divided into."
              },
              "tokenLimit": {
                "type": "integer",
                "minimum": 1,
                "description": "For tokenBucket: the most tokens the bucket holds."
              },
              "tokensPerPeriod": {
                "type": "integer",
                "minimum": 1,
                "description": "For tokenBucket: the number of tokens added each period."
              },
              "replenishmentPeriodSeconds": {
                "type": "integer",
                "minimum": 1,
                "description": "For tokenBucket: the length of a period in seconds."
              },
              "queueLimit": {
                "type": "integer",
                "minimum": 0,
                "description": "The number of requests over the limit that wait instead of getting a 429 response. The default is 0."
              },
              "queueProcessingOrder": {
                "type": "string",
                "enum": [
                  "oldestFirst",
                  "newestFirst"
                ],
                "description": "For concurrency and tokenBucket: which waiting request goes first. The default is oldestFirst."
              }
            },
            "required": [
              "type"
            ],
            "additionalProperties": false
          }
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates or updates a rate-limit policy: name, description, scope (global, endpoint path or group) and its limiters (fixed window, sliding window, concurrency, token bucket). It always saves a new policy as disabled (turn it on with enableRateLimitPolicy), and the target and limiters of an enabled policy cannot be changed until it is disabled.";

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
        var caller = AgentToolAuthorization.GetCaller(services);

        if (caller is null || !await AIToolCallerAuthorization.IsAuthorizedAsync(services, RateLimitsPermissions.ManageRateLimits))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!AgentToolArguments.TryReadJsonArray(arguments, "limiters", out var givenLimiters))
        {
            return "Nothing was saved: limiters must be a list of limiter objects.";
        }

        arguments.TryGetFirstString("policyId", out var policyId);

        var hasName = arguments.TryGetFirstString("name", true, out var name);
        var hasDescription = arguments.TryGetFirstString("description", true, out var description);
        var hasScope = arguments.TryGetFirstString("scope", out var scopeName);
        var hasPath = arguments.TryGetFirstString("path", out var path);
        var hasGroup = arguments.TryGetFirstString("group", out var group);
        var store = services.GetRequiredService<IRateLimitPolicyStore>();
        var create = string.IsNullOrWhiteSpace(policyId);
        RateLimitPolicy existing = null;

        name = name?.Trim();

        if (hasName && string.IsNullOrEmpty(name))
        {
            return "Nothing was saved: the policy name is required.";
        }

        if (create)
        {
            if (!hasName || !hasScope)
            {
                return "Nothing was saved: give name and scope for a new policy, or the policyId of the policy to update.";
            }
        }
        else
        {
            existing = await store.FindByIdAsync(policyId.Trim(), PolicyVersion.Current);

            if (existing is null)
            {
                return $"Nothing was saved: there is no rate-limit policy with the id '{policyId}'. Use listRateLimitPolicies to find it.";
            }

            if (!hasName && !hasDescription && !hasScope && !hasPath && !hasGroup && givenLimiters is null)
            {
                return "Nothing was saved: give at least one field to change.";
            }
        }

        var scope = existing?.Scope ?? RateLimitPolicyScope.Global;

        if (hasScope && !TryParseScope(scopeName, out scope))
        {
            return "Nothing was saved: scope must be global, endpoint or group.";
        }

        // As on the admin page, the target is the path of an endpoint policy or the group of a group policy.
        var targetPath = scope != RateLimitPolicyScope.Endpoint
            ? null
            : hasPath
                ? path.Trim()
                : existing?.Path;

        var targetGroup = scope != RateLimitPolicyScope.Group
            ? null
            : hasGroup
                ? group.Trim()
                : existing?.GroupName;

        if (existing?.IsEnabled == true &&
            (givenLimiters is not null ||
                scope != existing.Scope ||
                !string.Equals(targetPath, existing.Path, StringComparison.Ordinal) ||
                !string.Equals(targetGroup, existing.GroupName, StringComparison.Ordinal)))
        {
            return $"Nothing was saved: the policy '{existing.Name}' is enabled, and the scope, path, group and limiters of an enabled policy cannot be changed. Disable it first with disableRateLimitPolicy, or create another policy. Its name and description can be changed while it is enabled.";
        }

        var policies = await store.GetAllAsync(PolicyVersion.Current);

        // The checks of the Create and Edit pages.
        if (hasName && policies.Any(other =>
            !string.Equals(other.PolicyId, existing?.PolicyId, StringComparison.Ordinal) &&
            string.Equals(other.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            return $"Nothing was saved: a policy named '{name}' exists. Give its policyId to update it, or pick another name.";
        }

        if (scope == RateLimitPolicyScope.Endpoint)
        {
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                return "Nothing was saved: give path for an endpoint policy.";
            }

            if (!targetPath.StartsWith('/'))
            {
                return "Nothing was saved: the request path must start with '/'.";
            }
        }

        if (scope == RateLimitPolicyScope.Group && string.IsNullOrWhiteSpace(targetGroup))
        {
            return "Nothing was saved: give group for a group policy.";
        }

        List<RateLimitLimiter> limiters = null;

        if (givenLimiters is not null)
        {
            if (givenLimiters.Count > MaxLimiters)
            {
                return $"Nothing was saved: a policy gets at most {MaxLimiters} limiters from this tool.";
            }

            limiters = [];

            for (var index = 0; index < givenLimiters.Count; index++)
            {
                var (limiter, error) = BuildLimiter(services, givenLimiters[index] as JsonObject);

                if (error is not null)
                {
                    return $"Nothing was saved: limiter {index + 1}: {error}";
                }

                limiters.Add(limiter);
            }
        }

        // The fields the admin page sets. A new policy is disabled; an update keeps the status of the policy.
        var policy = new RateLimitPolicy
        {
            PolicyId = existing?.PolicyId ?? IdGenerator.GenerateId(),
            Name = hasName
                ? name
                : existing?.Name,
            Description = hasDescription
                ? description?.Trim()
                : existing?.Description,
            OwnerId = existing?.OwnerId ?? caller.FindFirstValue(ClaimTypes.NameIdentifier),
            Author = existing?.Author ?? caller.Identity?.Name,
            Scope = scope,
            Path = targetPath,
            GroupName = targetGroup,
            IsEnabled = existing?.IsEnabled ?? false,
            EnabledUtc = existing?.EnabledUtc,
        };

        policy.Limiters.AddRange(limiters ?? existing?.Limiters ?? []);

        if (create)
        {
            await store.CreateAsync(policy);
        }
        else
        {
            await store.UpdateAsync(policy);
        }

        var saved = await store.FindByIdAsync(policy.PolicyId, PolicyVersion.Current);

        if (saved is null)
        {
            return "Nothing was saved: the policy was not found after the save. Use listRateLimitPolicies to check.";
        }

        var warnings = GetWarnings(services, saved);

        return AgentToolResults.Json(new
        {
            saved = true,
            created = create,
            policy = await RateLimitToolSupport.DescribeAsync(services, saved),
            warnings = warnings.Count > 0
                ? warnings
                : null,
            note = saved.IsEnabled
                ? "The policy is enabled and keeps its scope and limiters."
                : "The policy is disabled and limits nothing yet. Tell the person its limits, then turn it on with enableRateLimitPolicy.",
        });
    }

    private static (RateLimitLimiter Limiter, string Error) BuildLimiter(IServiceProvider services, JsonObject given)
    {
        if (given is null)
        {
            return (null, "it must be an object with a type.");
        }

        var type = given["type"] is JsonValue typeValue && typeValue.TryGetValue<string>(out var typeText)
            ? typeText
            : null;

        var sourceName = RateLimitToolSupport.GetSourceName(type);

        if (sourceName is null)
        {
            return (null, $"type must be one of {string.Join(", ", RateLimitToolSupport.LimiterTypes)}.");
        }

        // The admin page offers a limiter type when its source is registered.
        if (RateLimitToolSupport.GetSource(services, sourceName) is null)
        {
            return (null, $"the limiter type '{type}' is not available on this site.");
        }

        if (!TryReadNumber(given, "queueLimit", 0, 0, out var queueLimit, out var error))
        {
            return (null, error);
        }

        var queueOrder = QueueProcessingOrder.OldestFirst;

        if (given["queueProcessingOrder"] is JsonValue orderValue && orderValue.TryGetValue<string>(out var orderText))
        {
            switch (orderText.Trim().ToLowerInvariant())
            {
                case "oldestfirst":
                    break;
                case "newestfirst":
                    queueOrder = QueueProcessingOrder.NewestFirst;
                    break;
                default:
                    return (null, "queueProcessingOrder must be oldestFirst or newestFirst.");
            }
        }

        var limiter = new RateLimitLimiter
        {
            Id = IdGenerator.GenerateId(),
            Source = sourceName,
        };

        // The numbers the limiter editors of the admin page require: each at least one. A limiter with a zero limit
        // or window cannot be built when the policy is enabled.
        switch (type.Trim().ToLowerInvariant())
        {
            case "fixedwindow":
                if (!TryReadNumber(given, "permitLimit", 1, null, out var fixedPermits, out error) ||
                    !TryReadNumber(given, "windowSeconds", 1, null, out var fixedWindow, out error))
                {
                    return (null, error);
                }

                limiter.Put(new FixedWindowRateLimiterData
                {
                    PermitLimit = fixedPermits,
                    WindowSeconds = fixedWindow,
                    QueueLimit = queueLimit,
                });

                break;

            case "slidingwindow":
                if (!TryReadNumber(given, "permitLimit", 1, null, out var slidingPermits, out error) ||
                    !TryReadNumber(given, "windowSeconds", 1, null, out var slidingWindow, out error) ||
                    !TryReadNumber(given, "segmentsPerWindow", 1, null, out var segments, out error))
                {
                    return (null, error);
                }

                limiter.Put(new SlidingWindowRateLimiterData
                {
                    PermitLimit = slidingPermits,
                    WindowSeconds = slidingWindow,
                    SegmentsPerWindow = segments,
                    QueueLimit = queueLimit,
                });

                break;

            case "concurrency":
                if (!TryReadNumber(given, "permitLimit", 1, null, out var concurrentPermits, out error))
                {
                    return (null, error);
                }

                limiter.Put(new ConcurrencyRateLimiterData
                {
                    PermitLimit = concurrentPermits,
                    QueueLimit = queueLimit,
                    QueueProcessingOrder = queueOrder,
                });

                break;

            default:
                if (!TryReadNumber(given, "tokenLimit", 1, null, out var tokenLimit, out error) ||
                    !TryReadNumber(given, "tokensPerPeriod", 1, null, out var tokensPerPeriod, out error) ||
                    !TryReadNumber(given, "replenishmentPeriodSeconds", 1, null, out var period, out error))
                {
                    return (null, error);
                }

                limiter.Put(new TokenBucketRateLimiterData
                {
                    TokenLimit = tokenLimit,
                    TokensPerPeriod = tokensPerPeriod,
                    ReplenishmentPeriodSeconds = period,
                    QueueLimit = queueLimit,
                    QueueProcessingOrder = queueOrder,
                });

                break;
        }

        return (limiter, null);
    }

    private static bool TryReadNumber(JsonObject given, string name, int minimum, int? fallback, out int value, out string error)
    {
        value = fallback ?? 0;
        error = null;

        var node = given[name];

        if (node is null)
        {
            if (fallback is not null)
            {
                return true;
            }

            error = $"give {name}, a whole number of at least {minimum}.";

            return false;
        }

        if (node is not JsonValue number || !number.TryGetValue<int>(out value) || value < minimum)
        {
            error = $"{name} must be a whole number of at least {minimum}.";

            return false;
        }

        return true;
    }

    private static bool TryParseScope(string name, out RateLimitPolicyScope scope)
    {
        scope = RateLimitPolicyScope.Global;

        switch (name.Trim().ToLowerInvariant())
        {
            case "global":
                return true;
            case "endpoint":
                scope = RateLimitPolicyScope.Endpoint;

                return true;
            case "group":
                scope = RateLimitPolicyScope.Group;

                return true;
            default:
                return false;
        }
    }

    private static List<string> GetWarnings(IServiceProvider services, RateLimitPolicy policy)
    {
        var warnings = new List<string>();

        if (policy.Limiters.Count == 0)
        {
            warnings.Add("The policy has no limiters, so it limits nothing.");
        }

        if (policy.Scope == RateLimitPolicyScope.Global)
        {
            warnings.Add("A global policy counts every request of every visitor, the admin area and this chat included. A limit that is too low locks everyone out; check the numbers with the person before the policy is enabled.");
        }

        if (policy.Scope == RateLimitPolicyScope.Group)
        {
            var groups = RateLimitToolSupport.GetEndpointGroups(services);

            if (!groups.Contains(policy.GroupName, StringComparer.OrdinalIgnoreCase))
            {
                warnings.Add(groups.Count == 0
                    ? $"No endpoint of the site is in the rate-limit group '{policy.GroupName}', so the policy would limit nothing."
                    : $"No endpoint of the site is in the rate-limit group '{policy.GroupName}', so the policy would limit nothing. The groups in use are: {string.Join(", ", groups)}.");
            }
        }

        return warnings;
    }
}
