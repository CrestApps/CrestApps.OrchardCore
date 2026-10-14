using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.TenantHierarchy.Core.Services;

/// <summary>
/// Builds the addresses of hierarchy tenants and checks return URLs. Addresses are built from shell settings only,
/// never from request input.
/// </summary>
public static class TenantHierarchyUrls
{
    /// <summary>
    /// Returns the scheme of tenant addresses: the configured one, or <c>https</c>, or in development the scheme of
    /// the current request.
    /// </summary>
    /// <param name="options">The tenant hierarchy options.</param>
    /// <param name="environment">The host environment.</param>
    /// <param name="request">The current request, or <see langword="null"/>.</param>
    public static string GetScheme(TenantHierarchyOptions options, IWebHostEnvironment environment, HttpRequest request)
    {
        if (!string.IsNullOrWhiteSpace(options?.Scheme))
        {
            return options.Scheme.Trim().ToLowerInvariant();
        }

        if (environment?.IsDevelopment() == true && !string.IsNullOrEmpty(request?.Scheme))
        {
            return request.Scheme;
        }

        return Uri.UriSchemeHttps;
    }

    /// <summary>
    /// Returns the base address of a tenant, for example <c>https://business1.firma.platform.com</c>, or
    /// <see langword="null"/> when the tenant has no host.
    /// </summary>
    /// <param name="settings">The shell settings of the tenant.</param>
    /// <param name="scheme">The scheme.</param>
    public static string GetBaseAddress(ShellSettings settings, string scheme)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var host = settings.GetPrimaryHost();

        if (string.IsNullOrEmpty(host))
        {
            return null;
        }

        var address = $"{scheme}://{host}";

        return string.IsNullOrEmpty(settings.RequestUrlPrefix)
            ? address
            : $"{address}/{settings.RequestUrlPrefix}";
    }

    /// <summary>
    /// Returns whether a return URL is local to the current site: it starts with a single <c>/</c> or with <c>~/</c>,
    /// and holds no backslash or control character.
    /// </summary>
    /// <param name="url">The URL.</param>
    public static bool IsLocalUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return false;
        }

        if (url.Any(character => char.IsControl(character) || character == '\\'))
        {
            return false;
        }

        if (url[0] == '/')
        {
            return url.Length == 1 || url[1] != '/';
        }

        return url.Length > 1 && url[0] == '~' && url[1] == '/' && (url.Length == 2 || url[2] != '/');
    }
}
