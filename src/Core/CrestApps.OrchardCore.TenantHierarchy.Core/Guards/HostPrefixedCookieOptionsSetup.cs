using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.TenantHierarchy.Core.Guards;

/// <summary>
/// Gives the authentication and antiforgery cookies of parent and child tenants the <c>__Host-</c> name prefix.
/// </summary>
/// <remarks>
/// A child tenant shares the domain of its parent, so a script on one child page could set a cookie for the parent's
/// domain that every sibling receives. A browser only accepts a <c>__Host-</c> cookie that is secure, has the root
/// path and has no domain, so no sibling can set or shadow these cookies.
/// </remarks>
internal sealed class HostPrefixedCookieOptionsSetup :
    IPostConfigureOptions<CookieAuthenticationOptions>,
    IPostConfigureOptions<AntiforgeryOptions>
{
    private readonly ShellSettings _shellSettings;
    private readonly bool _enabled;

    /// <summary>
    /// Initializes a new instance of the <see cref="HostPrefixedCookieOptionsSetup"/> class.
    /// </summary>
    /// <param name="shellSettings">The settings of the current tenant.</param>
    /// <param name="options">The tenant hierarchy options.</param>
    /// <param name="environment">The host environment.</param>
    public HostPrefixedCookieOptionsSetup(
        ShellSettings shellSettings,
        IOptions<TenantHierarchyOptions> options,
        IHostEnvironment environment)
    {
        _shellSettings = shellSettings;
        _enabled = IsEnabled(options.Value, environment);
    }

    /// <summary>
    /// Returns whether the <c>__Host-</c> cookie names are on: as configured, or on everywhere except in development.
    /// </summary>
    /// <param name="options">The tenant hierarchy options.</param>
    /// <param name="environment">The host environment.</param>
    internal static bool IsEnabled(TenantHierarchyOptions options, IHostEnvironment environment)
        => options?.UseHostPrefixedCookies ?? environment?.IsDevelopment() != true;

    /// <inheritdoc/>
    public void PostConfigure(string name, CookieAuthenticationOptions options)
    {
        if (name != IdentityConstants.ApplicationScheme || !AppliesToTenant())
        {
            return;
        }

        Harden(options.Cookie, $"orchauth_{_shellSettings.Name}");
    }

    /// <inheritdoc/>
    public void PostConfigure(string name, AntiforgeryOptions options)
    {
        if (!AppliesToTenant())
        {
            return;
        }

        Harden(options.Cookie, $"orchantiforgery_{_shellSettings.Name}");
    }

    /// <summary>
    /// Returns the cookie name with the <c>__Host-</c> prefix.
    /// </summary>
    /// <param name="name">The cookie name.</param>
    internal static string GetHostPrefixedName(string name)
    {
        return name.StartsWith(TenantHierarchyConstants.Cookies.HostPrefix, StringComparison.Ordinal)
            ? name
            : TenantHierarchyConstants.Cookies.HostPrefix + name;
    }

    private bool AppliesToTenant()
    {
        return _enabled &&
            _shellSettings.IsInTenantHierarchy() &&
            string.IsNullOrEmpty(_shellSettings.RequestUrlPrefix);
    }

    private static void Harden(CookieBuilder cookie, string defaultName)
    {
        cookie.Name = GetHostPrefixedName(string.IsNullOrEmpty(cookie.Name) ? defaultName : cookie.Name);
        cookie.Path = "/";
        cookie.Domain = null;
        cookie.SecurePolicy = CookieSecurePolicy.Always;
    }
}
