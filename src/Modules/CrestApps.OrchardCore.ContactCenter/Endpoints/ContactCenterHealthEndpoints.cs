using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CrestApps.OrchardCore.ContactCenter.Endpoints;

/// <summary>
/// Exposes the Contact Center readiness and dependency probes.
/// </summary>
/// <remarks>
/// The two routes answer two different questions and must never share an implementation.
/// <list type="bullet">
/// <item><description>Readiness answers "should this node receive traffic for this tenant".</description></item>
/// <item><description>The dependency probe answers "is anything degraded, and what".</description></item>
/// </list>
/// <para>
/// Liveness is deliberately absent here. It answers "should this process be restarted", which no tenant-scoped
/// route can answer: it would return 404 whenever the tenant is disabled or moved, and an orchestrator reads
/// that as a probe failure. It is served by host middleware instead — see
/// <c>UseContactCenterProcessLiveness</c>.
/// </para>
/// <para>
/// Aggregating dependency checks into readiness turns a degraded dependency into a total outage: a condition
/// every node shares is observed by every node, so every node drains at the same moment and the load balancer
/// is left with no target. Readiness therefore selects only node-local state, and dependency health is reported
/// on its own authorized route for alerting and dashboards.
/// </para>
/// <para>
/// The <c>OrchardCore.HealthChecks</c> module, when enabled, maps a single endpoint with no registration
/// predicate, so it aggregates every check registered by every module. That endpoint is therefore neither a
/// liveness nor a readiness signal despite its default <c>/health/live</c> route. Operators should point probes
/// at the routes mapped here instead.
/// </para>
/// </remarks>
internal static class ContactCenterHealthEndpoints
{
    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Maps the Contact Center readiness and dependency probes.
    /// </summary>
    /// <param name="builder">The endpoint route builder to map the probes onto.</param>
    /// <returns>The same <paramref name="builder"/> so calls can be chained.</returns>
    public static IEndpointRouteBuilder AddContactCenterHealthEndpoints(this IEndpointRouteBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.MapHealthChecks(ContactCenterConstants.HealthChecks.ReadinessRoute, new HealthCheckOptions
        {
            Predicate = IsReadinessCheck,
            AllowCachingResponses = false,
        }).AllowAnonymous();

        builder.MapGet(ContactCenterConstants.HealthChecks.DependenciesRoute, ReportDependenciesAsync);

        return builder;
    }

    /// <summary>
    /// Determines whether a registration participates in the readiness probe.
    /// </summary>
    /// <param name="registration">The health check registration under consideration.</param>
    /// <returns><see langword="true"/> when the registration is tagged as a readiness check.</returns>
    /// <remarks>
    /// Only node-local checks carry the readiness tag. A registration that consults a shared dependency must
    /// not be selected here, because it evaluates identically on every node and would drain the fleet at once.
    /// </remarks>
    internal static bool IsReadinessCheck(HealthCheckRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        return registration.Tags.Contains(ContactCenterConstants.HealthChecks.ReadyTag);
    }

    /// <summary>
    /// Determines whether a registration participates in the dependency probe.
    /// </summary>
    /// <param name="registration">The health check registration under consideration.</param>
    /// <returns><see langword="true"/> when the registration is tagged as a dependency check.</returns>
    internal static bool IsDependencyCheck(HealthCheckRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        return registration.Tags.Contains(ContactCenterConstants.HealthChecks.DependencyTag);
    }

    /// <summary>
    /// Writes the per-check dependency report for an authorized caller.
    /// </summary>
    /// <param name="httpContext">The current request, used for authorization and to write the response.</param>
    /// <param name="healthCheckService">The service that evaluates the dependency checks.</param>
    /// <param name="authorizationService">The service used to authorize the caller.</param>
    /// <returns>A task that completes when the response has been written.</returns>
    /// <remarks>
    /// Unlike the liveness and readiness probes this route discloses which dependency is degraded, so it
    /// requires <see cref="ContactCenterPermissions.MonitorContactCenter"/>. Exception detail is never written:
    /// a connection failure message can carry a connection string. The response is always 200 when authorized,
    /// because the degraded verdict is the payload rather than a transport-level failure.
    /// </remarks>
    internal static async Task ReportDependenciesAsync(
        HttpContext httpContext,
        [FromServices] HealthCheckService healthCheckService,
        [FromServices] IAuthorizationService authorizationService)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(healthCheckService);
        ArgumentNullException.ThrowIfNull(authorizationService);

        if (!await authorizationService.AuthorizeAsync(httpContext.User, ContactCenterPermissions.MonitorContactCenter))
        {
            httpContext.Response.StatusCode = httpContext.User?.Identity?.IsAuthenticated == true
                ? StatusCodes.Status403Forbidden
                : StatusCodes.Status401Unauthorized;

            return;
        }

        var report = await healthCheckService.CheckHealthAsync(IsDependencyCheck, httpContext.RequestAborted);

        httpContext.Response.StatusCode = StatusCodes.Status200OK;
        httpContext.Response.ContentType = "application/json";
        httpContext.Response.Headers.CacheControl = "no-store, no-cache";

        var payload = new
        {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration,
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
                duration = entry.Value.Duration,
                data = entry.Value.Data,
            }),
        };

        await JsonSerializer.SerializeAsync(httpContext.Response.Body, payload, _serializerOptions, httpContext.RequestAborted);
    }
}
