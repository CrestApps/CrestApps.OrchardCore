using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using OrchardCore.Autoroute.Core.Indexes;
using OrchardCore.ContentManagement.Routing;
using OrchardCore.Environment.Extensions;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Subscriptions.Core.Services;

/// <summary>
/// Answers whether the store already serves URLs under a first path segment: one of its routes starts with it, one of
/// its pages' paths does, it is a module's or theme's id or a top-level entry of the web root, whose static files
/// Orchard Core serves under <c>/{segment}/</c> without an endpoint, or one of its authentication schemes answers there,
/// as a remote sign-in's callback does, also without an endpoint. A site sold on that prefix of the store's address would
/// take those URLs from the store.
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
    private readonly IAuthenticationSchemeProvider _schemes;
    private readonly IAuthenticationHandlerProvider _handlers;
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="StoreRouteSegments"/> class.
    /// </summary>
    /// <param name="endpoints">The store's endpoints.</param>
    /// <param name="autorouteEntries">The Autoroute entries, present only while Autoroute is enabled.</param>
    /// <param name="session">The store's session, which reads its pages' paths.</param>
    /// <param name="extensionManager">The modules and themes, whose static files are served under their ids.</param>
    /// <param name="environment">The host environment, whose web root is served from the root of every address.</param>
    /// <param name="schemes">The store's authentication schemes, whose remote sign-in callbacks have no endpoint.</param>
    /// <param name="handlers">The authentication handlers, which hold each scheme's options.</param>
    /// <param name="httpContextAccessor">The current request, or the one Orchard Core creates for background work.</param>
    public StoreRouteSegments(
        EndpointDataSource endpoints,
        IEnumerable<IAutorouteEntries> autorouteEntries,
        ISession session,
        IExtensionManager extensionManager,
        IWebHostEnvironment environment,
        IAuthenticationSchemeProvider schemes,
        IAuthenticationHandlerProvider handlers,
        IHttpContextAccessor httpContextAccessor)
    {
        _endpoints = endpoints;
        _autorouteEntries = autorouteEntries;
        _session = session;
        _extensionManager = extensionManager;
        _environment = environment;
        _schemes = schemes;
        _handlers = handlers;
        _httpContextAccessor = httpContextAccessor;
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
            IsStaticFileFolder(segment) ||
            IsNamedLikeARemoteSignInCallback(segment) ||
            await IsAnAuthenticationPathAsync(segment))
        {
            return true;
        }

        // The Autoroute index exists only while Autoroute is enabled, and so does its entries service.
        if (!_autorouteEntries.Any())
        {
            return false;
        }

        // The database compares text in its own collation, which is exact on PostgreSQL (YesSql makes SQLite's text columns
        // NOCASE), so a page whose path is "Pricing" was not found for "pricing". The query asks for the segment, with or
        // without a leading slash, in the spellings a permalink takes; the first segments of the paths are compared here,
        // without case. Asking for every path that starts with the segment's first letter read most of a large store's pages.
        var underSegment = PathIsUnder(Spellings(segment));

        for (var skip = 0; ; skip += PageSize)
        {
            var pages = (await _session
                .QueryIndex(underSegment)
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

    // ASP.NET Core names a remote sign-in's callback signin-{provider}, and OpenID Connect's sign-out callbacks signout-...,
    // so a provider the operator turns on after the site was sold would answer there too.
    private static bool IsNamedLikeARemoteSignInCallback(string segment)
        => segment.StartsWith("signin-", StringComparison.OrdinalIgnoreCase) ||
            segment.StartsWith("signout-", StringComparison.OrdinalIgnoreCase);

    // Before routing, the authentication middleware lets every scheme that handles requests answer on the paths its options
    // name: a remote sign-in's callback, OpenID Connect's sign-out callbacks. The options are the scheme's own, so a callback
    // path the operator changed is read as changed. Background work runs with the HttpContext Orchard Core creates for it.
    private async Task<bool> IsAnAuthenticationPathAsync(string segment)
    {
        if (_httpContextAccessor.HttpContext is not { } httpContext)
        {
            return false;
        }

        foreach (var scheme in await _schemes.GetRequestHandlerSchemesAsync())
        {
            if (OptionsOf(await _handlers.GetHandlerAsync(httpContext, scheme.Name)) is not { } options)
            {
                continue;
            }

            foreach (var property in options.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.PropertyType == typeof(PathString) &&
                    property.GetIndexParameters().Length == 0 &&
                    property.GetValue(options) is PathString { HasValue: true } path &&
                    FirstSegmentIs(path.Value, segment))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static object OptionsOf(IAuthenticationHandler handler)
    {
        for (var type = handler?.GetType(); type is not null; type = type.BaseType)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(AuthenticationHandler<>))
            {
                return type.GetProperty(nameof(AuthenticationHandler<AuthenticationSchemeOptions>.Options)).GetValue(handler);
            }
        }

        return null;
    }

    // The segment as typed, in lower and upper case, capitalized, and with each word capitalized ("Getting-Started").
    private static HashSet<string> Spellings(string segment)
    {
        var lower = segment.ToLowerInvariant();
        var words = lower.ToCharArray();

        for (var i = 0; i < words.Length; i++)
        {
            if (i == 0 || words[i - 1] is '-' or '_')
            {
                words[i] = char.ToUpperInvariant(words[i]);
            }
        }

        return new HashSet<string>(StringComparer.Ordinal)
        {
            segment,
            lower,
            segment.ToUpperInvariant(),
            char.ToUpperInvariant(lower[0]) + lower[1..],
            new string(words),
        };
    }

    // index => index.Path == "{s}" || index.Path.StartsWith("{s}/") || index.Path == "/{s}" || index.Path.StartsWith("/{s}/"),
    // for each spelling.
    private static Expression<Func<AutoroutePartIndex, bool>> PathIsUnder(IEnumerable<string> spellings)
    {
        var index = Expression.Parameter(typeof(AutoroutePartIndex), "index");
        var path = Expression.Property(index, nameof(AutoroutePartIndex.Path));
        var startsWith = typeof(string).GetMethod(nameof(string.StartsWith), [typeof(string)]);
        Expression body = null;

        foreach (var spelling in spellings)
        {
            foreach (var rooted in new[] { spelling, "/" + spelling })
            {
                var term = Expression.OrElse(
                    Expression.Equal(path, Expression.Constant(rooted)),
                    Expression.Call(path, startsWith, Expression.Constant(rooted + "/")));

                body = body is null ? term : Expression.OrElse(body, term);
            }
        }

        return Expression.Lambda<Func<AutoroutePartIndex, bool>>(body, index);
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
