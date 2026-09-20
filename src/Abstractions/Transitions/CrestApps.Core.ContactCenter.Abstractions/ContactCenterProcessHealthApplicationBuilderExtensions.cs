using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.Core.ContactCenter;

/// <summary>
/// Adds the process liveness probe to the host pipeline.
/// </summary>
/// <remarks>
/// Liveness must be answered by the process itself, ahead of the host's own pipeline, because it answers
/// exactly one question: should this process be restarted. A probe mapped inside one tenant of a multi-tenant
/// host cannot answer that. It returns 404 whenever the tenant is disabled, renamed, given a different request
/// URL prefix, or fails to start — and an orchestrator reads 404 as a probe failure, so a healthy process is
/// restarted for a tenant-level problem, forever.
/// <para>
/// This is deliberately a short-circuiting middleware rather than a mapped endpoint. Endpoints registered on a
/// <see cref="WebApplication"/> are executed by terminal middleware appended after everything the application
/// added, so an endpoint would be evaluated after the host's own pipeline had already handled the request.
/// </para>
/// </remarks>
public static class ContactCenterProcessHealthApplicationBuilderExtensions
{
    /// <summary>
    /// Answers the process liveness probe before the request reaches the Orchard Core pipeline.
    /// </summary>
    /// <param name="app">The application builder to add the middleware to.</param>
    /// <returns>The same <paramref name="app"/> so calls can be chained.</returns>
    /// <remarks>
    /// Call this before the host adds its own pipeline. The probe consults nothing: reaching it already proves
    /// the process is scheduling requests, which is the only claim a liveness probe may make.
    /// <para>
    /// The path is deliberately not a parameter here. It is supplied once to
    /// <c>AddContactCenterProcessLiveness</c>, which is also what validates it against every configured tenant.
    /// Accepting a path here as well would allow the middleware to answer on a path that was never validated,
    /// which is exactly the collision this probe exists to prevent.
    /// </para>
    /// </remarks>
    public static IApplicationBuilder UseContactCenterProcessLiveness(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var options = app.ApplicationServices.GetService<ContactCenterProcessLivenessOptions>()
            ?? throw new InvalidOperationException(
                "The Contact Center process liveness probe has not been registered. Call " +
                "'services.AddContactCenterProcessLiveness()' before building the application, so the reserved " +
                "path is validated against every configured tenant. Without it, a tenant that maps its health " +
                "endpoint on the same path would be silently shadowed by an unconditional '200 Healthy'.");

        // Where the host's own aggregate health endpoint lives is the host's knowledge - which configuration
        // key names it, and what it falls back to - so the host supplies the answer rather than this package
        // reading a key it would have to name.
        ThrowIfShadowsSharedHealthEndpoint(
            options.Path,
            options.SharedHealthEndpointRouteResolver?.Invoke(app.ApplicationServices),
            tenantName: null);

        var livenessPath = new PathString(options.Path);

        return app.Use(async (context, next) =>
        {
            if (!IsLivenessRequest(context, livenessPath))
            {
                await next(context);

                return;
            }

            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "text/plain";
            context.Response.Headers.CacheControl = "no-store, no-cache";

            await context.Response.WriteAsync("Healthy", context.RequestAborted);
        });
    }

    /// <summary>
    /// Throws when the liveness path would shadow the host's own shared health endpoint.
    /// </summary>
    /// <param name="livenessPath">The path this middleware will answer.</param>
    /// <param name="sharedEndpointRoute">
    /// The host's shared health endpoint route, already resolved to the value it will actually serve on. A
    /// host with no shared health endpoint passes <see langword="null"/>, and nothing is checked.
    /// </param>
    /// <param name="tenantName">The tenant the route was configured on, or <see langword="null"/> for the host.</param>
    /// <exception cref="InvalidOperationException">The two paths are the same.</exception>
    /// <remarks>
    /// This middleware short-circuits before routing, so a collision does not surface as a duplicate-route
    /// error. It surfaces as the shared endpoint silently answering an unconditional <c>200 Healthy</c> for
    /// every tenant in the process, including tenants that never enable Contact Center. A permanently healthy
    /// health endpoint is worse than no health endpoint, so the collision fails startup instead.
    /// </remarks>
    public static void ThrowIfShadowsSharedHealthEndpoint(
        string livenessPath,
        string sharedEndpointRoute,
        string tenantName)
    {
        if (string.IsNullOrWhiteSpace(sharedEndpointRoute))
        {
            return;
        }

        var effectiveSharedRoute = sharedEndpointRoute.Trim();

        if (!Normalize(livenessPath).Equals(Normalize(effectiveSharedRoute), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var scope = tenantName is null
            ? "the host configuration"
            : $"tenant '{tenantName}'";

        throw new InvalidOperationException(
            $"The Contact Center process liveness probe is configured at '{livenessPath}', which is the same " +
            $"route as the host's shared health endpoint ('{effectiveSharedRoute}') in {scope}. Liveness runs " +
            "as host middleware ahead of routing, so it would shadow that endpoint for every tenant in this " +
            "process — including tenants that do not enable Contact Center — and answer an unconditional " +
            "'200 Healthy' in its place. Move one of them: pass a different path to " +
            "AddContactCenterProcessLiveness, or configure the shared health endpoint on another route.");
    }

    /// <summary>
    /// Normalizes a route for comparison by trimming whitespace and surrounding slashes.
    /// </summary>
    /// <param name="route">The route to normalize.</param>
    /// <returns>The normalized route.</returns>
    private static string Normalize(string route)
        => route.Trim().Trim('/');

    /// <summary>
    /// Determines whether the request targets the liveness probe.
    /// </summary>
    /// <param name="context">The request under consideration.</param>
    /// <param name="livenessPath">The configured liveness path.</param>
    /// <returns><see langword="true"/> when the request is a readable probe of the liveness path.</returns>
    /// <remarks>
    /// Only GET and HEAD are answered. A liveness probe never mutates, and answering every verb would make the
    /// path a silent 200 for requests an operator would expect to be rejected.
    /// </remarks>
    private static bool IsLivenessRequest(HttpContext context, PathString livenessPath)
        => (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method))
            && context.Request.Path.Equals(livenessPath, StringComparison.OrdinalIgnoreCase);
}
