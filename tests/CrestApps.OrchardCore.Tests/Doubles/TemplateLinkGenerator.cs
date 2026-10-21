using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.Tests.Doubles;

/// <summary>
/// A link generator that builds each route's path with a function of the route name and values, the way the endpoint
/// the route names would, so a test can check the links a service makes without the routing system.
/// </summary>
public sealed class TemplateLinkGenerator : LinkGenerator
{
    private readonly Func<string, RouteValueDictionary, string> _path;

    /// <summary>
    /// Initializes a new instance of the <see cref="TemplateLinkGenerator"/> class.
    /// </summary>
    /// <param name="path">Returns the path of a route name and values, or <see langword="null"/> for a route that isn't known.</param>
    public TemplateLinkGenerator(Func<string, RouteValueDictionary, string> path)
    {
        _path = path;
    }

    /// <summary>
    /// Returns a generator that knows one route, whose path is the prefix followed by the escaped token.
    /// </summary>
    /// <param name="routeName">The route's name.</param>
    /// <param name="prefix">The path before the token, such as <c>/downloads/</c>.</param>
    /// <param name="suffix">The path after the token, if any.</param>
    public static TemplateLinkGenerator ForToken(string routeName, string prefix, string suffix = "")
        => new((name, values) => string.Equals(name, routeName, StringComparison.Ordinal)
            ? prefix + Uri.EscapeDataString(values["token"]?.ToString() ?? string.Empty) + suffix
            : null);

    /// <inheritdoc />
    public override string GetPathByAddress<TAddress>(HttpContext httpContext, TAddress address, RouteValueDictionary values, RouteValueDictionary ambientValues = null, PathString? pathBase = null, FragmentString fragment = default, LinkOptions options = null)
        => Path(address, values) is { } path ? (pathBase ?? httpContext?.Request.PathBase ?? PathString.Empty) + path : null;

    /// <inheritdoc />
    public override string GetPathByAddress<TAddress>(TAddress address, RouteValueDictionary values, PathString pathBase = default, FragmentString fragment = default, LinkOptions options = null)
        => Path(address, values) is { } path ? pathBase + path : null;

    /// <inheritdoc />
    public override string GetUriByAddress<TAddress>(HttpContext httpContext, TAddress address, RouteValueDictionary values, RouteValueDictionary ambientValues = null, string scheme = null, HostString? host = null, PathString? pathBase = null, FragmentString fragment = default, LinkOptions options = null)
        => Path(address, values) is { } path
            ? $"{scheme ?? httpContext.Request.Scheme}://{host ?? httpContext.Request.Host}{pathBase ?? httpContext.Request.PathBase}{path}"
            : null;

    /// <inheritdoc />
    public override string GetUriByAddress<TAddress>(TAddress address, RouteValueDictionary values, string scheme, HostString host, PathString pathBase = default, FragmentString fragment = default, LinkOptions options = null)
        => Path(address, values) is { } path ? $"{scheme}://{host}{pathBase}{path}" : null;

    private string Path<TAddress>(TAddress address, RouteValueDictionary values)
        => _path((address as RouteValuesAddress)?.RouteName, values);
}
