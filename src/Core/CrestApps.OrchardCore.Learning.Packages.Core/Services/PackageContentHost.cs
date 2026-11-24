using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.Learning.Packages.Core.Services;

/// <summary>
/// Which host answers a request, when a tenant serves its package files from a package content host of their own.
/// </summary>
public enum PackageHostRoute
{
    /// <summary>
    /// The site answers the request as usual.
    /// </summary>
    Site,

    /// <summary>
    /// A package file or a package frame page on the package content host.
    /// </summary>
    PackageContent,

    /// <summary>
    /// The request is refused with 404.
    /// </summary>
    Refused,
}

/// <summary>
/// The package content host: an origin of its own (for example <c>https://packages.example.com</c>) that serves only the
/// files of learning packages and their frame pages. Without one, every package file is served from the site's own
/// host under a sandbox without <c>allow-same-origin</c>, so each page and frame of a package runs on an opaque origin
/// of its own (strict mode). Packages that run in several frames that reach each other (Articulate Rise and
/// Storyline 360) need the content host: there the files run on the content host's origin, so the frames of a package
/// share it, while the site's cookies, pages and endpoints stay on another origin.
/// </summary>
public static class PackageContentHost
{
    /// <summary>
    /// The path of the package files, under which the launch token and the file path follow.
    /// </summary>
    public const string RunPath = "/learning/packages/run/";

    /// <summary>
    /// The path of the frame page of a launch on the content host, under which the launch token follows.
    /// </summary>
    public const string FramePath = "/learning/packages/frame/";

    /// <summary>
    /// The policy of every package file in strict mode: scripts, forms and popups, and no same origin.
    /// </summary>
    public const string StrictSandboxPolicy = "sandbox allow-scripts allow-forms allow-popups";

    /// <summary>
    /// The <c>sandbox</c> attribute of the player's frame in strict mode.
    /// </summary>
    public const string StrictFrameSandbox = "allow-scripts allow-forms allow-popups";

    /// <summary>
    /// The <c>sandbox</c> attribute of the player's frame on the content host: the frames of a package share the content
    /// host's origin, which is not the site's, and a window the package opens is not sandboxed.
    /// </summary>
    public const string ContentHostFrameSandbox = "allow-scripts allow-same-origin allow-forms allow-popups allow-popups-to-escape-sandbox";

    /// <summary>
    /// The name of the item a request on the content host carries in <see cref="HttpContext.Items"/>.
    /// </summary>
    public const string ContentRequestItem = "CrestApps.OrchardCore.Learning.Packages.ContentHostRequest";

    /// <summary>
    /// Makes an origin of a base URL.
    /// </summary>
    /// <param name="url">The base URL: <c>http</c> or <c>https</c>, a host and an optional port, and no path, query,
    /// fragment or user information.</param>
    /// <param name="origin">The origin, <c>scheme://host[:port]</c> in lower case without the default port.</param>
    /// <returns><see langword="true"/> when the URL is an origin.</returns>
    public static bool TryNormalizeOrigin(string url, out string origin)
    {
        origin = null;

        if (string.IsNullOrWhiteSpace(url) ||
            !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            uri.AbsolutePath != "/" ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment) ||
            uri.OriginalString.Trim().Contains('#', StringComparison.Ordinal) ||
            uri.OriginalString.Trim().Contains('?', StringComparison.Ordinal) ||
            string.IsNullOrEmpty(uri.Host))
        {
            return false;
        }

        origin = uri.GetLeftPart(UriPartial.Authority).ToLowerInvariant();

        return true;
    }

    /// <summary>
    /// Tells whether a request's host is the content host.
    /// </summary>
    /// <param name="contentOrigin">The content host's origin, or <see langword="null"/>.</param>
    /// <param name="host">The request's host.</param>
    /// <returns><see langword="true"/> when the host and port are the content host's.</returns>
    public static bool IsContentHost(string contentOrigin, HostString host)
    {
        if (string.IsNullOrEmpty(contentOrigin) || !host.HasValue || !Uri.TryCreate(contentOrigin, UriKind.Absolute, out var origin))
        {
            return false;
        }

        return string.Equals(origin.Host, host.Host, StringComparison.OrdinalIgnoreCase) &&
            (host.Port ?? origin.Port) == origin.Port;
    }

    /// <summary>
    /// Tells whether a path is a package file or a package frame page.
    /// </summary>
    /// <param name="path">The request path, relative to the tenant.</param>
    /// <returns><see langword="true"/> for a package path.</returns>
    public static bool IsPackagePath(PathString path)
        => path.StartsWithSegments(RunPath.TrimEnd('/'), StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments(FramePath.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Decides which host answers a request.
    /// </summary>
    /// <param name="contentOrigin">The content host's origin, or <see langword="null"/> in strict mode.</param>
    /// <param name="host">The request's host.</param>
    /// <param name="path">The request path, relative to the tenant.</param>
    /// <returns>The route.</returns>
    public static PackageHostRoute Route(string contentOrigin, HostString host, PathString path)
    {
        var packagePath = IsPackagePath(path);

        if (string.IsNullOrEmpty(contentOrigin))
        {
            // Strict mode: the files are on the site's host, and the frame page of the content host does not exist.
            return packagePath && path.StartsWithSegments(FramePath.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)
                ? PackageHostRoute.Refused
                : PackageHostRoute.Site;
        }

        if (IsContentHost(contentOrigin, host))
        {
            return packagePath ? PackageHostRoute.PackageContent : PackageHostRoute.Refused;
        }

        return packagePath ? PackageHostRoute.Refused : PackageHostRoute.Site;
    }

    /// <summary>
    /// Builds the policy of a package file or frame page on the content host.
    /// </summary>
    /// <param name="playerOrigin">The origin of the player page that launched the package, from the launch token.</param>
    /// <returns>The sandbox with the same origin, and <c>frame-ancestors</c> limited to the content host and the player.</returns>
    public static string ContentHostPolicy(string playerOrigin)
    {
        var policy = "sandbox " + ContentHostFrameSandbox + "; frame-ancestors 'self'";

        return TryNormalizeOrigin(playerOrigin, out var origin) && string.Equals(origin, playerOrigin, StringComparison.OrdinalIgnoreCase)
            ? policy + " " + origin
            : policy;
    }
}
