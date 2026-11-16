using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using OrchardCore.Autoroute.Core.Indexes;
using OrchardCore.ContentManagement.Routing;
using OrchardCore.Environment.Extensions;
using YesSql;

namespace CrestApps.OrchardCore.Subscriptions.Core.Services;

/// <summary>
/// Answers whether the store already serves URLs under a first path segment: one of its routes starts with it, one of
/// its pages' paths does, or it is a module's or theme's id or a top-level entry of the web root, whose static files
/// Orchard Core serves under <c>/{segment}/</c> without an endpoint. A site sold on that prefix of the store's address
/// would take those URLs from the store.
/// </summary>
/// <remarks>
/// Orchard Core builds the endpoints of a tenant on the tenant's own container, so the <see cref="EndpointDataSource"/>
/// resolved here lists the routes of the store this runs on, the ones Orchard's own Rate Limits admin also lists.
/// </remarks>
public sealed class StoreRouteSegments
{
    private const int PageSize = 500;

    private readonly EndpointDataSource _endpoints;
    private readonly IEnumerable<IAutorouteEntries> _autorouteEntries;
    private readonly ISession _session;
    private readonly IExtensionManager _extensionManager;
    private readonly IWebHostEnvironment _environment;

    /// <summary>
    /// Initializes a new instance of the <see cref="StoreRouteSegments"/> class.
    /// </summary>
    /// <param name="endpoints">The store's endpoints.</param>
    /// <param name="autorouteEntries">The Autoroute entries, present only while Autoroute is enabled.</param>
    /// <param name="session">The store's session, which reads its pages' paths.</param>
    /// <param name="extensionManager">The modules and themes, whose static files are served under their ids.</param>
    /// <param name="environment">The host environment, whose web root is served from the root of every address.</param>
    public StoreRouteSegments(
        EndpointDataSource endpoints,
        IEnumerable<IAutorouteEntries> autorouteEntries,
        ISession session,
        IExtensionManager extensionManager,
        IWebHostEnvironment environment)
    {
        _endpoints = endpoints;
        _autorouteEntries = autorouteEntries;
        _session = session;
        _extensionManager = extensionManager;
        _environment = environment;
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

        if (_endpoints.Endpoints.OfType<RouteEndpoint>().Any(endpoint => StartsWith(endpoint.RoutePattern, segment)) ||
            IsStaticFileFolder(segment))
        {
            return true;
        }

        // The Autoroute index exists only while Autoroute is enabled, and so does its entries service.
        if (!_autorouteEntries.Any())
        {
            return false;
        }

        // The database compares text in its own collation, which is exact on PostgreSQL (YesSql makes SQLite's text columns
        // NOCASE), so a page whose path is "Pricing" was not found for "pricing". The query only narrows the rows to the
        // segment's first character in either case; the first segments of the paths are compared here, without case.
        var lower = char.ToLowerInvariant(segment[0]).ToString();
        var upper = char.ToUpperInvariant(segment[0]).ToString();
        var rootedLower = "/" + lower;
        var rootedUpper = "/" + upper;

        for (var skip = 0; ; skip += PageSize)
        {
            var pages = (await _session
                .QueryIndex<AutoroutePartIndex>(index =>
                    index.Path.StartsWith(lower) ||
                    index.Path.StartsWith(upper) ||
                    index.Path.StartsWith(rootedLower) ||
                    index.Path.StartsWith(rootedUpper))
                .OrderBy(index => index.Id)
                .Skip(skip)
                .Take(PageSize)
                .ListAsync(cancellationToken))
                .ToList();

            if (pages.Any(page => FirstSegmentIs(page.Path, segment)))
            {
                return true;
            }

            if (pages.Count < PageSize)
            {
                return false;
            }
        }
    }

    // Orchard Core serves a module's or theme's wwwroot under /{module}/, and the host's web root from the root, with the
    // static file middleware, so neither has an endpoint.
    private bool IsStaticFileFolder(string segment)
    {
        if (_extensionManager.GetExtensions().Any(extension => string.Equals(extension.Id, segment, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var webRoot = _environment.WebRootFileProvider?.GetDirectoryContents(string.Empty);

        return webRoot is { Exists: true } &&
            webRoot.Any(entry => string.Equals(entry.Name, segment, StringComparison.OrdinalIgnoreCase));
    }

    private static bool FirstSegmentIs(string path, string segment)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        var trimmed = path.AsSpan().TrimStart('/');
        var end = trimmed.IndexOf('/');

        return (end < 0 ? trimmed : trimmed[..end]).Equals(segment, StringComparison.OrdinalIgnoreCase);
    }

    private static bool StartsWith(RoutePattern pattern, string segment)
        => pattern?.PathSegments.Count > 0 &&
            pattern.PathSegments[0] is { IsSimple: true } first &&
            first.Parts[0] is RoutePatternLiteralPart literal &&
            string.Equals(literal.Content, segment, StringComparison.OrdinalIgnoreCase);
}
