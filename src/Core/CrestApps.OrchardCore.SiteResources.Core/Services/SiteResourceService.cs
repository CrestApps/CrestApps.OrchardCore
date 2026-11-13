using System.Security.Cryptography;
using System.Text;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.SiteResources.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NUglify;
using OrchardCore.Documents;

namespace CrestApps.OrchardCore.SiteResources.Services;

/// <summary>
/// Reads the published site resources from their cached document, works out the ones a page gets, and minifies and
/// versions a resource when it is saved.
/// </summary>
public sealed class SiteResourceService : ISiteResourceService
{
    private readonly IDocumentManager<DictionaryDocument<SiteResource>> _documentManager;
    private readonly IEnumerable<ISiteResourcesHandler> _handlers;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SiteResourceService"/> class.
    /// </summary>
    /// <param name="documentManager">The document the site resources catalog is stored in.</param>
    /// <param name="handlers">The handlers that may change the resources a page gets.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="logger">The logger.</param>
    public SiteResourceService(
        IDocumentManager<DictionaryDocument<SiteResource>> documentManager,
        IEnumerable<ISiteResourcesHandler> handlers,
        IHttpContextAccessor httpContextAccessor,
        ILogger<SiteResourceService> logger)
    {
        _documentManager = documentManager;
        _handlers = handlers;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SiteResourceEntry>> GetPageResourcesAsync(bool isAdmin, string themeId)
    {
        // The cached document is shared and read on every page, so its records are used as they are instead of being
        // copied; nothing here changes them.
        var document = await _documentManager.GetOrCreateImmutableAsync();

        var context = new SiteResourcesResolvingContext(
            isAdmin,
            themeId,
            document.Records.Values
                .Where(resource => resource.IsOfferedTo(isAdmin, themeId) && !string.IsNullOrEmpty(resource.Name))
                .Select(resource => new SiteResourceEntry(resource, GetUrl(resource))));

        foreach (var handler in _handlers)
        {
            try
            {
                await handler.ResolvingAsync(context);
            }
            catch (Exception ex)
            {
                // The page renders with the published resources rather than failing.
                _logger.LogWarning(ex, "The site resources handler {Handler} failed.", handler.GetType().Name);
            }
        }

        return context.Entries
            .Where(entry => entry.Resource.IsOfferedTo(isAdmin, themeId))
            .OrderBy(entry => entry.Resource.Position)
            .ThenBy(entry => entry.Resource.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <inheritdoc />
    public string GetUrl(SiteResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);

        var pathBase = _httpContextAccessor.HttpContext?.Request.PathBase.Value ?? string.Empty;

        return GetUrl(pathBase, SiteResourcesConstants.RoutePrefix, resource);
    }

    /// <summary>
    /// Gets the address a resource is served from under <paramref name="pathBase"/> and <paramref name="routePrefix"/>.
    /// </summary>
    /// <param name="pathBase">The tenant's path base, such as <c>/tenant1</c>, or empty.</param>
    /// <param name="routePrefix">The path the resources are served from, without slashes.</param>
    /// <param name="resource">The resource.</param>
    public static string GetUrl(string pathBase, string routePrefix, SiteResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);

        var url = $"{pathBase}/{routePrefix}/{Uri.EscapeDataString(resource.Name)}.{resource.GetExtension()}";

        return string.IsNullOrEmpty(resource.Version)
            ? url
            : $"{url}?v={resource.Version}";
    }

    /// <inheritdoc />
    public IReadOnlyList<string> Prepare(SiteResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);

        var (served, errors) = Minify(resource.Type, resource.Content, resource.Minify);

        resource.ServedContent = served;
        resource.Version = ComputeVersion(served);

        return errors;
    }

    /// <summary>
    /// Minifies <paramref name="content"/> when <paramref name="minify"/> is on.
    /// </summary>
    /// <param name="type">Whether the content is CSS or JavaScript.</param>
    /// <param name="content">The content as it was written.</param>
    /// <param name="minify">Whether to minify it.</param>
    /// <returns>What to serve, and the minifier's errors. On errors the content is served as it was written.</returns>
    public static (string Served, IReadOnlyList<string> Errors) Minify(SiteResourceType type, string content, bool minify)
    {
        content ??= string.Empty;

        if (!minify || string.IsNullOrWhiteSpace(content))
        {
            return (content, []);
        }

        var result = type == SiteResourceType.Script
            ? Uglify.Js(content)
            : Uglify.Css(content);

        var errors = result.Errors
            .Where(error => error.IsError)
            .Select(error => $"Line {error.StartLine}, column {error.StartColumn}: {error.Message}")
            .ToArray();

        return errors.Length > 0 || result.Code is null
            ? (content, errors)
            : (result.Code, []);
    }

    /// <summary>
    /// Gets a short hash of the served content, used as the version in the resource's address.
    /// </summary>
    /// <param name="served">The served content.</param>
    public static string ComputeVersion(string served)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(served ?? string.Empty)))[..12];
}
