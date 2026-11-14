using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using OrchardCore.Autoroute.Core.Indexes;
using OrchardCore.ContentManagement.Routing;
using YesSql;

namespace CrestApps.OrchardCore.Subscriptions.Core.Services;

/// <summary>
/// Answers whether the store already serves URLs under a first path segment: one of its routes starts with it, or one
/// of its pages' paths does. A site sold on that prefix of the store's address would take those URLs from the store.
/// </summary>
/// <remarks>
/// Orchard Core builds the endpoints of a tenant on the tenant's own container, so the <see cref="EndpointDataSource"/>
/// resolved here lists the routes of the store this runs on, the ones Orchard's own Rate Limits admin also lists.
/// </remarks>
public sealed class StoreRouteSegments
{
    private readonly EndpointDataSource _endpoints;
    private readonly IEnumerable<IAutorouteEntries> _autorouteEntries;
    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="StoreRouteSegments"/> class.
    /// </summary>
    /// <param name="endpoints">The store's endpoints.</param>
    /// <param name="autorouteEntries">The Autoroute entries, present only while Autoroute is enabled.</param>
    /// <param name="session">The store's session, which reads its pages' paths.</param>
    public StoreRouteSegments(
        EndpointDataSource endpoints,
        IEnumerable<IAutorouteEntries> autorouteEntries,
        ISession session)
    {
        _endpoints = endpoints;
        _autorouteEntries = autorouteEntries;
        _session = session;
    }

    /// <summary>
    /// Whether the store serves URLs whose first segment is <paramref name="segment"/>, compared without case.
    /// </summary>
    /// <param name="segment">The first path segment, without slashes.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<bool> IsTakenAsync(string segment, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(segment))
        {
            return false;
        }

        if (_endpoints.Endpoints.OfType<RouteEndpoint>().Any(endpoint => StartsWith(endpoint.RoutePattern, segment)))
        {
            return true;
        }

        // The Autoroute index exists only while Autoroute is enabled, and so does its entries service.
        if (!_autorouteEntries.Any())
        {
            return false;
        }

        var path = segment.ToLowerInvariant();
        var under = path + "/";
        var rooted = "/" + path;
        var rootedUnder = rooted + "/";

        var page = await _session
            .QueryIndex<AutoroutePartIndex>(index =>
                index.Path == path ||
                index.Path == rooted ||
                index.Path.StartsWith(under) ||
                index.Path.StartsWith(rootedUnder))
            .FirstOrDefaultAsync(cancellationToken);

        return page is not null;
    }

    private static bool StartsWith(RoutePattern pattern, string segment)
        => pattern?.PathSegments.Count > 0 &&
            pattern.PathSegments[0] is { IsSimple: true } first &&
            first.Parts[0] is RoutePatternLiteralPart literal &&
            string.Equals(literal.Content, segment, StringComparison.OrdinalIgnoreCase);
}
