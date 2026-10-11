using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.TenantHierarchy.Core.Guards;

/// <summary>
/// Reads the Fetch Metadata request headers a browser sends, so the tenant hierarchy can refuse requests that a script
/// on another page made.
/// </summary>
public static class FetchMetadataPolicy
{
    /// <summary>
    /// The header that tells how the request was made.
    /// </summary>
    public const string ModeHeader = "Sec-Fetch-Mode";

    /// <summary>
    /// The header that tells where the request came from.
    /// </summary>
    public const string SiteHeader = "Sec-Fetch-Site";

    /// <summary>
    /// Returns whether a request is a top-level or frame navigation, or comes from a client that sends no Fetch
    /// Metadata. A script cannot make such a request with <c>fetch</c>, or with an image or script tag.
    /// </summary>
    /// <param name="request">The request.</param>
    public static bool IsNavigationOrUnknown(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var mode = request.Headers[ModeHeader].ToString();

        return string.IsNullOrEmpty(mode) ||
            string.Equals(mode, "navigate", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns whether a request to a parent tenant must be refused because it came from a page on another site of the
    /// same registrable domain, such as one of its child tenants, and is not a navigation.
    /// </summary>
    /// <param name="request">The request.</param>
    public static bool IsSameSiteSubresource(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var site = request.Headers[SiteHeader].ToString();

        return string.Equals(site, "same-site", StringComparison.OrdinalIgnoreCase) &&
            !IsNavigationOrUnknown(request);
    }
}
