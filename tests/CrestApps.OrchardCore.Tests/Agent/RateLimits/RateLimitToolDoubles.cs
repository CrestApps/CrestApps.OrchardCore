using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell;
using OrchardCore.RateLimits;
using OrchardCore.RateLimits.Core;
using OrchardCore.RateLimits.Models;

namespace CrestApps.OrchardCore.Tests.Agent.RateLimits;

/// <summary>
/// Stands in for the Rate Limits services in a tool test: policies kept in memory behind the policy store, the four
/// limiter sources, the built-in limits modules register, the endpoints of the site with their rate-limit groups, and
/// the service that asks for the tenant to reload.
/// </summary>
internal sealed class RateLimitToolDoubles
{
    private readonly Mock<IShellReleaseManager> _releaseManager = new();

    public RateLimitToolDoubles()
    {
        // An enabled global policy: it limits every request, sign-in included.
        Global = Add("policy-global", "Whole site", RateLimitPolicyScope.Global, enabled: true, FixedWindow(1000, 60));

        // An enabled policy on the group of the sign-in endpoints.
        LoginThrottle = Add("policy-login", "Login throttle", RateLimitPolicyScope.Group, enabled: true, FixedWindow(10, 60));
        LoginThrottle.GroupName = "password-authentication";

        // An enabled policy on a path that has nothing to do with sign-in.
        ContactApi = Add("policy-contact", "Contact API", RateLimitPolicyScope.Endpoint, enabled: true, FixedWindow(10, 60));
        ContactApi.Path = "/api/contact";
        ContactApi.Description = "Protects the contact form.";

        // A disabled policy.
        Draft = Add("policy-draft", "Draft policy", RateLimitPolicyScope.Endpoint, enabled: false, FixedWindow(5, 30));
        Draft.Path = "/api/search";
    }

    public RateLimitPolicy Global { get; }

    public RateLimitPolicy LoginThrottle { get; }

    public RateLimitPolicy ContactApi { get; }

    public RateLimitPolicy Draft { get; }

    /// <summary>
    /// Gets the policies the store keeps.
    /// </summary>
    public List<RateLimitPolicy> Policies { get; } = [];

    /// <summary>
    /// Gets the number of times a tool asked for the tenant to reload.
    /// </summary>
    public int ReleaseRequests
        => _releaseManager.Invocations.Count(invocation => invocation.Method.Name == nameof(IShellReleaseManager.RequestRelease));

    /// <summary>
    /// Adds the doubles to the services of a tool call.
    /// </summary>
    /// <param name="services">The services of the tool call.</param>
    public void AddTo(IServiceCollection services)
    {
        services.AddSingleton<IRateLimitPolicyStore>(new InMemoryPolicyStore(Policies));
        services.AddSingleton(_releaseManager.Object);

        foreach (var sourceName in new[] { "FixedWindow", "SlidingWindow", "Concurrency", "TokenBucket" })
        {
            services.AddKeyedSingleton<IRateLimiterSource>(sourceName, new FakeLimiterSource(sourceName));
        }

        var options = new RateLimitsOptions();
        options.AddRouteRateLimit("Login", HttpMethods.Post, _ => RateLimitPartition.GetNoLimiter("login"));
        options.AddGroupRateLimit("password-authentication", _ => RateLimitPartition.GetNoLimiter("group"));

        services.AddSingleton(Options.Create(options));

        // The sign-in endpoint of the Users module carries the group of the sign-in endpoints.
        services.AddSingleton<EndpointDataSource>(new DefaultEndpointDataSource(
            new RouteEndpoint(
                _ => Task.CompletedTask,
                RoutePatternFactory.Parse("Login"),
                0,
                new EndpointMetadataCollection(new RateLimitGroupAttribute("password-authentication")),
                "Login"),
            new RouteEndpoint(
                _ => Task.CompletedTask,
                RoutePatternFactory.Parse("api/contact"),
                0,
                new EndpointMetadataCollection(new RateLimitGroupAttribute("contact-forms")),
                "Contact")));
    }

    /// <summary>
    /// Creates a fixed window limiter as the store keeps it.
    /// </summary>
    /// <param name="permitLimit">The number of requests allowed in a window.</param>
    /// <param name="windowSeconds">The length of the window in seconds.</param>
    /// <returns>The limiter.</returns>
    public static RateLimitLimiter FixedWindow(int permitLimit, int windowSeconds)
    {
        var limiter = new RateLimitLimiter
        {
            Id = $"limiter-{permitLimit}-{windowSeconds}",
            Source = "FixedWindow",
        };

        limiter.Put(new FixedWindowRateLimiterData
        {
            PermitLimit = permitLimit,
            WindowSeconds = windowSeconds,
        });

        return limiter;
    }

    private RateLimitPolicy Add(string id, string name, RateLimitPolicyScope scope, bool enabled, params RateLimitLimiter[] limiters)
    {
        var policy = new RateLimitPolicy
        {
            PolicyId = id,
            Name = name,
            Scope = scope,
            IsEnabled = enabled,
            EnabledUtc = enabled
                ? new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc)
                : null,
            OwnerId = "user-owner",
            Author = "owner",
        };

        policy.Limiters.AddRange(limiters);
        Policies.Add(policy);

        return policy;
    }

    private sealed class InMemoryPolicyStore : IRateLimitPolicyStore
    {
        private readonly List<RateLimitPolicy> _policies;

        public InMemoryPolicyStore(List<RateLimitPolicy> policies)
        {
            _policies = policies;
        }

        public ValueTask<RateLimitPolicy> FindByIdAsync(string policyId, PolicyVersion version)
            => ValueTask.FromResult(_policies.FirstOrDefault(policy => policy.PolicyId == policyId && (version == PolicyVersion.Current || policy.IsEnabled)));

        public ValueTask<IReadOnlyCollection<RateLimitPolicy>> GetAllAsync(PolicyVersion version)
            => ValueTask.FromResult<IReadOnlyCollection<RateLimitPolicy>>(_policies.Where(policy => version == PolicyVersion.Current || policy.IsEnabled).ToList());

        public ValueTask CreateAsync(RateLimitPolicy policy)
        {
            _policies.Add(policy);

            return ValueTask.CompletedTask;
        }

        public ValueTask UpdateAsync(RateLimitPolicy policy)
        {
            var index = _policies.FindIndex(stored => stored.PolicyId == policy.PolicyId);

            _policies[index] = policy;

            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> DeleteAsync(RateLimitPolicy policy)
            => ValueTask.FromResult(_policies.RemoveAll(stored => stored.PolicyId == policy.PolicyId) > 0);

        public ValueTask<bool> SetStatusAsync(string policyId, bool isEnabled)
        {
            var policy = _policies.FirstOrDefault(stored => stored.PolicyId == policyId);

            if (policy is null)
            {
                return ValueTask.FromResult(false);
            }

            policy.IsEnabled = isEnabled;
            policy.EnabledUtc = isEnabled
                ? new DateTime(2026, 11, 17, 9, 0, 0, DateTimeKind.Utc)
                : null;

            return ValueTask.FromResult(true);
        }
    }

    private sealed class FakeLimiterSource : IRateLimiterSource
    {
        public FakeLimiterSource(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public LocalizedString DisplayName
            => new(Name, Name);

        public LocalizedString Description
            => new(Name, Name);

        public RateLimitPartition<string> CreatePartition(string policyName, HttpContext context, RateLimitLimiter limiter)
            => RateLimitPartition.GetNoLimiter(policyName);

        public string Describe(RateLimitLimiter limiter)
            => $"{Name} limiter";
    }
}
