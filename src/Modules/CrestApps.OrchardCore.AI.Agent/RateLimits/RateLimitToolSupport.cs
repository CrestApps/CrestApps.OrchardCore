using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Entities;
using OrchardCore.Modules;
using OrchardCore.RateLimits;
using OrchardCore.RateLimits.Core;
using OrchardCore.RateLimits.Models;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.AI.Agent.RateLimits;

/// <summary>
/// What the rate-limit tools share: the limiter types the Rate Limits admin page offers, what a tool returns about a
/// policy, the IDs a tool that acts on several policies was given, and whether a policy protects the sign-in and
/// account endpoints.
/// </summary>
internal static class RateLimitToolSupport
{
    /// <summary>
    /// The largest number of policies one call of a tool that takes a list acts on.
    /// </summary>
    public const int MaxPolicies = 20;

    /// <summary>
    /// The name of the fixed window limiter in the arguments and results of the tools.
    /// </summary>
    public const string FixedWindow = "fixedWindow";

    /// <summary>
    /// The name of the sliding window limiter in the arguments and results of the tools.
    /// </summary>
    public const string SlidingWindow = "slidingWindow";

    /// <summary>
    /// The name of the concurrency limiter in the arguments and results of the tools.
    /// </summary>
    public const string Concurrency = "concurrency";

    /// <summary>
    /// The name of the token bucket limiter in the arguments and results of the tools.
    /// </summary>
    public const string TokenBucket = "tokenBucket";

    // The limiter sources of the Rate Limits module, by the name a tool uses. A source is registered under its name;
    // a tool checks that it is before it stores a limiter of that source.
    private static readonly Dictionary<string, string> _sourceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        [FixedWindow] = "FixedWindow",
        [SlidingWindow] = "SlidingWindow",
        [Concurrency] = "Concurrency",
        [TokenBucket] = "TokenBucket",
    };

    // The rate-limit groups the Users module puts on its sign-in and account endpoints.
    private static readonly string[] _signInGroups =
    [
        UserRateLimiterPolicyNames.PasswordAuthentication,
        UserRateLimiterPolicyNames.TwoFactorAuthentication,
        UserRateLimiterPolicyNames.TwoFactorRecovery,
        UserRateLimiterPolicyNames.TwoFactorCodeSend,
        UserRateLimiterPolicyNames.PasswordRecovery,
        UserRateLimiterPolicyNames.UserRegistration,
        UserRateLimiterPolicyNames.EmailConfirmation,
    ];

    /// <summary>
    /// Gets the limiter types of the tools.
    /// </summary>
    public static IEnumerable<string> LimiterTypes
        => _sourceNames.Keys;

    /// <summary>
    /// Gets the name of the limiter source of the Rate Limits module for a limiter type of the tools.
    /// </summary>
    /// <param name="type">The limiter type of the tools.</param>
    /// <returns>The source name, or <see langword="null"/> when there is no such type.</returns>
    public static string GetSourceName(string type)
    {
        return type is not null && _sourceNames.TryGetValue(type.Trim(), out var sourceName)
            ? sourceName
            : null;
    }

    /// <summary>
    /// Gets the limiter source that is registered under a name.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="sourceName">The name of the source.</param>
    /// <returns>The source, or <see langword="null"/> when none is registered under the name.</returns>
    public static IRateLimiterSource GetSource(IServiceProvider services, string sourceName)
    {
        return string.IsNullOrEmpty(sourceName) || services is not IKeyedServiceProvider
            ? null
            : services.GetKeyedService<IRateLimiterSource>(sourceName);
    }

    /// <summary>
    /// Gets the name of a scope in the arguments and results of the tools.
    /// </summary>
    /// <param name="scope">The scope of a policy.</param>
    /// <returns>The name: global, endpoint or group.</returns>
    public static string GetScopeName(RateLimitPolicyScope scope)
    {
        return scope switch
        {
            RateLimitPolicyScope.Endpoint => "endpoint",
            RateLimitPolicyScope.Group => "group",
            _ => "global",
        };
    }

    /// <summary>
    /// Reads the IDs a tool that acts on several policies was given.
    /// </summary>
    /// <param name="arguments">The arguments of the tool invocation.</param>
    /// <param name="ids">The IDs without blanks and repeats.</param>
    /// <returns>The sentence the tool returns when the IDs are missing or too many, or <see langword="null"/>.</returns>
    public static string ReadPolicyIds(AIFunctionArguments arguments, out List<string> ids)
    {
        if (!AgentToolArguments.TryReadStrings(arguments, "policyIds", out ids) || ids is null || ids.Count == 0)
        {
            return "give policyIds, a list with the id of each policy. Use listRateLimitPolicies to get the ids.";
        }

        return ids.Count > MaxPolicies
            ? $"one call acts on at most {MaxPolicies} policies."
            : null;
    }

    /// <summary>
    /// Builds what a tool returns about one policy, from an explicit list of fields.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="policy">The policy.</param>
    /// <returns>The fields the tool returns.</returns>
    public static async Task<object> DescribeAsync(IServiceProvider services, RateLimitPolicy policy)
    {
        var localClock = services.GetRequiredService<ILocalClock>();

        return new
        {
            id = policy.PolicyId,
            name = policy.Name,
            description = policy.Description,
            scope = GetScopeName(policy.Scope),
            path = policy.Scope == RateLimitPolicyScope.Endpoint
                ? policy.Path
                : null,
            group = policy.Scope == RateLimitPolicyScope.Group
                ? policy.GroupName
                : null,
            enabled = policy.IsEnabled,
            enabledAt = policy.IsEnabled
                ? await AgentToolResults.ToLocalAsync(localClock, policy.EnabledUtc)
                : null,
            limiters = policy.Limiters.Select(limiter => DescribeLimiter(services, limiter)).ToList(),
            protectsSignIn = GetSignInProtection(services, policy) is null
                ? (bool?)null
                : true,
            adminUrl = GetAdminUrl(services, policy.PolicyId),
        };
    }

    /// <summary>
    /// Says why a policy protects the sign-in and account endpoints, when it is enabled and does.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="policy">The policy.</param>
    /// <returns>The reason, to finish a sentence that starts with the name of the policy, or <see langword="null"/>.</returns>
    public static string GetSignInProtection(IServiceProvider services, RateLimitPolicy policy)
    {
        if (!policy.IsEnabled || policy.Limiters.Count == 0)
        {
            return null;
        }

        switch (policy.Scope)
        {
            case RateLimitPolicyScope.Global:
                return "is a global policy, so it limits every request, sign-in included";

            case RateLimitPolicyScope.Group:
                return _signInGroups.Contains(policy.GroupName, StringComparer.OrdinalIgnoreCase)
                    ? $"limits the group '{policy.GroupName}' of the sign-in and account endpoints"
                    : null;

            case RateLimitPolicyScope.Endpoint:
                var signInPath = FindSignInPath(services, policy.Path);

                return signInPath is null
                    ? null
                    : $"limits the path '{policy.Path}', which covers the sign-in or account endpoint '{signInPath}'";

            default:
                return null;
        }
    }

    /// <summary>
    /// Gets the rate-limit groups the endpoints of the site carry, the ones the Rate Limits admin page offers for a
    /// group policy.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <returns>The group names, in alphabetical order.</returns>
    public static List<string> GetEndpointGroups(IServiceProvider services)
    {
        var endpoints = services.GetService<EndpointDataSource>()?.Endpoints ?? [];

        return endpoints
            .SelectMany(endpoint => endpoint.Metadata.OfType<IRateLimitGroupMetadata>())
            .SelectMany(metadata => metadata.GroupNames)
            .Where(groupName => !string.IsNullOrWhiteSpace(groupName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Gets the address of the Edit page of a policy.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="policyId">The ID of the policy.</param>
    /// <returns>The address, or <see langword="null"/> when the request cannot build one.</returns>
    public static string GetAdminUrl(IServiceProvider services, string policyId)
    {
        return AgentToolLinks.GetAdminPath(services, OrchardCoreFeatureIds.RateLimits, "Admin", "Edit", new RouteValueDictionary
        {
            { "policyId", policyId },
        });
    }

    private static object DescribeLimiter(IServiceProvider services, RateLimitLimiter limiter)
    {
        var type = _sourceNames.FirstOrDefault(pair => string.Equals(pair.Value, limiter.Source, StringComparison.Ordinal)).Key;

        // What each source stores, as the editors of the Rate Limits admin page show it.
        object settings = type switch
        {
            FixedWindow => DescribeFixedWindow(limiter.GetOrCreate<FixedWindowRateLimiterData>()),
            SlidingWindow => DescribeSlidingWindow(limiter.GetOrCreate<SlidingWindowRateLimiterData>()),
            Concurrency => DescribeConcurrency(limiter.GetOrCreate<ConcurrencyRateLimiterData>()),
            TokenBucket => DescribeTokenBucket(limiter.GetOrCreate<TokenBucketRateLimiterData>()),
            _ => null,
        };

        return new
        {
            id = limiter.Id,
            type = type ?? limiter.Source,

            // The sentence the admin page shows for the limiter.
            summary = GetSource(services, limiter.Source)?.Describe(limiter),
            settings,
        };
    }

    private static object DescribeFixedWindow(FixedWindowRateLimiterData data)
    {
        return new
        {
            permitLimit = data.PermitLimit,
            windowSeconds = data.WindowSeconds,
            queueLimit = data.QueueLimit,
        };
    }

    private static object DescribeSlidingWindow(SlidingWindowRateLimiterData data)
    {
        return new
        {
            permitLimit = data.PermitLimit,
            windowSeconds = data.WindowSeconds,
            segmentsPerWindow = data.SegmentsPerWindow,
            queueLimit = data.QueueLimit,
        };
    }

    private static object DescribeConcurrency(ConcurrencyRateLimiterData data)
    {
        return new
        {
            permitLimit = data.PermitLimit,
            queueLimit = data.QueueLimit,
            queueProcessingOrder = GetQueueOrderName(data.QueueProcessingOrder),
        };
    }

    private static object DescribeTokenBucket(TokenBucketRateLimiterData data)
    {
        return new
        {
            tokenLimit = data.TokenLimit,
            tokensPerPeriod = data.TokensPerPeriod,
            replenishmentPeriodSeconds = data.ReplenishmentPeriodSeconds,
            queueLimit = data.QueueLimit,
            queueProcessingOrder = GetQueueOrderName(data.QueueProcessingOrder),
        };
    }

    private static string GetQueueOrderName(QueueProcessingOrder order)
    {
        return order == QueueProcessingOrder.NewestFirst
            ? "newestFirst"
            : "oldestFirst";
    }

    private static string FindSignInPath(IServiceProvider services, string policyPath)
    {
        if (string.IsNullOrWhiteSpace(policyPath) || !policyPath.StartsWith('/'))
        {
            return null;
        }

        // The root covers every path.
        if (policyPath.Trim('/').Length == 0)
        {
            return "/";
        }

        var prefix = new PathString(policyPath.TrimEnd('/'));
        var endpoints = services.GetService<EndpointDataSource>()?.Endpoints ?? [];

        foreach (var endpoint in endpoints.OfType<RouteEndpoint>())
        {
            var groups = endpoint.Metadata
                .OfType<IRateLimitGroupMetadata>()
                .SelectMany(metadata => metadata.GroupNames);

            if (!groups.Any(groupName => _signInGroups.Contains(groupName, StringComparer.OrdinalIgnoreCase)))
            {
                continue;
            }

            var pattern = endpoint.RoutePattern.RawText;

            if (string.IsNullOrWhiteSpace(pattern))
            {
                continue;
            }

            var path = "/" + pattern.TrimStart('~').TrimStart('/');

            // The match the rate limiter makes for an endpoint policy: the request path starts with the policy path.
            if (new PathString(path).StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }
        }

        return null;
    }
}
