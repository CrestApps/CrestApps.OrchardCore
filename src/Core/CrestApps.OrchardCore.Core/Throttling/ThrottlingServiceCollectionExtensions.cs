using System.Globalization;
using System.Text.Json;
using System.Threading.RateLimiting;
using CrestApps.OrchardCore.Throttling;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OrchardCore.RateLimits;

namespace CrestApps.OrchardCore.Core.Throttling;

/// <summary>
/// Registers the attempt limiter and its policies, and adds group rate limits and 429 bodies to Orchard Core's Rate
/// Limits feature. Never write another attempt counter or 429 response: register a policy or a group limit.
/// </summary>
public static class ThrottlingServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IAttemptLimiter"/>. Safe to call more than once.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection AddAttemptLimiter(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<AttemptLimitOptions>();

        // Every tenant reads its overrides, whichever modules registered the policies.
        services.TryAddEnumerable(ServiceDescriptor.Transient<IPostConfigureOptions<AttemptLimitOptions>, AttemptPolicyConfiguration>());
        services.TryAddScoped<IAttemptLimiter, DistributedAttemptLimiter>();

        return services;
    }

    /// <summary>
    /// Registers an attempt policy with its defaults, overridable by configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="name">The policy name.</param>
    /// <param name="permitLimit">How many attempts a key may have in a window.</param>
    /// <param name="window">The window.</param>
    /// <param name="blockDuration">How long a key that reached the limit stays blocked.</param>
    /// <param name="resetOnSuccess">Whether a success clears the counter.</param>
    public static IServiceCollection AddAttemptPolicy(this IServiceCollection services, string name, int permitLimit, TimeSpan window, TimeSpan blockDuration = default, bool resetOnSuccess = false)
    {
        services.AddAttemptLimiter();
        services.Configure<AttemptLimitOptions>(options => options.AddPolicy(name, permitLimit, window, blockDuration, resetOnSuccess));

        return services;
    }

    /// <summary>
    /// Applies the overrides in a configuration section (by policy name) after the code registrations. A tenant's own
    /// <c>CrestApps_Throttling:AttemptPolicies</c> section is applied by <see cref="AddAttemptLimiter"/> already; this is for
    /// another section.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="section">The <c>CrestApps_Throttling:AttemptPolicies</c> section.</param>
    public static IServiceCollection AddAttemptPolicyOverrides(this IServiceCollection services, IConfiguration section)
    {
        ArgumentNullException.ThrowIfNull(section);

        services.PostConfigure<AttemptLimitOptions>(options => AttemptPolicyConfiguration.Apply(section, options));

        return services;
    }

    /// <summary>
    /// Adds a built-in group limit, partitioned by <see cref="ClientNetworkKey"/>. It shows read-only under
    /// <b>Tools &gt; Rate Limits</b>. Call it only from a startup that requires the Rate Limits feature.
    /// </summary>
    /// <param name="options">The rate limit options.</param>
    /// <param name="groupName">The group the endpoints are tagged with.</param>
    /// <param name="permitLimit">How many requests a client may make in the window.</param>
    /// <param name="window">The window.</param>
    /// <param name="networkOnly">Whether to count by network even for a signed-in user.</param>
    public static RateLimitsOptions AddClientGroupRateLimit(this RateLimitsOptions options, string groupName, int permitLimit, TimeSpan window, bool networkOnly = false)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(groupName);

        // Two modules may share a group, such as the provider webhooks; the first registration stands.
        if (options.GroupRateLimits.Any(limit => string.Equals(limit.GroupName, groupName, StringComparison.OrdinalIgnoreCase)))
        {
            return options;
        }

        options.AddGroupRateLimit(groupName, RateLimitPartitionHelpers.CreateFixedWindowPolicy(
            groupName,
            permitLimit,
            window,
            networkOnly ? ClientNetworkKey.NetworkOnly : ClientNetworkKey.From));

        return options;
    }

    /// <summary>
    /// Answers a refused request on the named routes with one problem+json 429 body and a <c>Retry-After</c> header.
    /// </summary>
    /// <param name="options">The rate limit options.</param>
    /// <param name="routeNames">The routes.</param>
    public static RateLimitsOptions AddJsonRejectionHandler(this RateLimitsOptions options, params string[] routeNames)
    {
        ArgumentNullException.ThrowIfNull(options);

        foreach (var routeName in routeNames ?? [])
        {
            options.AddRouteRejectionHandler(routeName, WriteProblemAsync);
        }

        return options;
    }

    /// <summary>
    /// Writes a 429 problem+json response with a <c>Retry-After</c> header.
    /// </summary>
    /// <param name="httpContext">The request.</param>
    /// <param name="retryAfter">How long to wait.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public static async Task WriteTooManyRequestsAsync(HttpContext httpContext, TimeSpan retryAfter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));

        httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        httpContext.Response.Headers.RetryAfter = seconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            type = "https://tools.ietf.org/html/rfc6585#section-4",
            title = "Too many requests.",
            status = StatusCodes.Status429TooManyRequests,
            detail = "Too many attempts. Wait a moment, then try again.",

            // The storefront scripts (checkout-payment.js) show errorMessage; without it a buyer sees a generic failure.
            errorMessage = "Too many attempts. Wait a moment, then try again.",
            retryAfter = seconds,
        }), cancellationToken);
    }

    private static ValueTask WriteProblemAsync(Microsoft.AspNetCore.RateLimiting.OnRejectedContext context, CancellationToken cancellationToken)
    {
        var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var value) ? value : TimeSpan.FromSeconds(60);

        return new ValueTask(WriteTooManyRequestsAsync(context.HttpContext, retryAfter, cancellationToken));
    }
}
