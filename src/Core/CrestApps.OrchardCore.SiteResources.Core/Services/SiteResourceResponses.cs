using CrestApps.OrchardCore.SiteResources.Models;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.SiteResources.Services;

/// <summary>
/// Builds the response that serves a site resource, shared by this module's endpoint and any other module that serves
/// site resources.
/// </summary>
public static class SiteResourceResponses
{
    /// <summary>
    /// How long a browser keeps a resource whose address carries its current version: a year.
    /// </summary>
    public const int ImmutableMaxAgeSeconds = 31536000;

    /// <summary>
    /// Splits a requested file name such as <c>site.css</c> into the resource name and its type.
    /// </summary>
    /// <param name="fileName">The requested file name.</param>
    /// <param name="name">The resource name.</param>
    /// <param name="type">The resource type the extension stands for.</param>
    /// <returns>Whether the file name is a valid resource name with a <c>.css</c> or <c>.js</c> extension.</returns>
    public static bool TryParseFileName(string fileName, out string name, out SiteResourceType type)
    {
        name = null;
        type = default;

        if (string.IsNullOrEmpty(fileName))
        {
            return false;
        }

        var extensionIndex = fileName.LastIndexOf('.');

        if (extensionIndex <= 0)
        {
            return false;
        }

        var extension = fileName[(extensionIndex + 1)..];

        if (string.Equals(extension, "css", StringComparison.OrdinalIgnoreCase))
        {
            type = SiteResourceType.Stylesheet;
        }
        else if (string.Equals(extension, "js", StringComparison.OrdinalIgnoreCase))
        {
            type = SiteResourceType.Script;
        }
        else
        {
            return false;
        }

        name = fileName[..extensionIndex];

        return SiteResourceNames.IsValid(name);
    }

    /// <summary>
    /// Writes the resource's served content, answering <c>304 Not Modified</c> when the browser has it already.
    /// </summary>
    /// <param name="httpContext">The HTTP context.</param>
    /// <param name="resource">The resource to serve.</param>
    /// <param name="requestedVersion">The version in the requested address, if any.</param>
    /// <param name="isPrivate">Whether shared caches must not keep the response, as for admin resources.</param>
    public static IResult Serve(HttpContext httpContext, SiteResource resource, string requestedVersion, bool isPrivate)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(resource);

        var headers = httpContext.Response.Headers;
        var etag = $"\"{resource.Version}\"";

        // An address with the current version never changes, so it is cached for a year. Any other address (no
        // version, or an old one a page still links to) is checked again on every use.
        headers.CacheControl = !string.IsNullOrEmpty(resource.Version) && string.Equals(requestedVersion, resource.Version, StringComparison.Ordinal)
            ? $"{(isPrivate ? "private" : "public")}, max-age={ImmutableMaxAgeSeconds}, immutable"
            : $"{(isPrivate ? "private" : "public")}, no-cache";
        headers.ETag = etag;
        headers.XContentTypeOptions = "nosniff";

        if (httpContext.Request.Headers.IfNoneMatch.Any(value => string.Equals(value, etag, StringComparison.Ordinal)))
        {
            return Results.StatusCode(StatusCodes.Status304NotModified);
        }

        var contentType = resource.Type == SiteResourceType.Script
            ? "text/javascript; charset=utf-8"
            : "text/css; charset=utf-8";

        return Results.Text(resource.ServedContent ?? resource.Content ?? string.Empty, contentType);
    }
}
