using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Doubles;

/// <summary>
/// Builds the signed token service and the site URL generator the link services use.
/// </summary>
public static class SignedLinkDoubles
{
    /// <summary>
    /// Returns a signed token service on the given keys and clock, or on new keys and the current time.
    /// </summary>
    /// <param name="protection">The data protection provider, or <see langword="null"/> for new keys.</param>
    /// <param name="clock">The clock, or <see langword="null"/> for one at the current time.</param>
    public static SignedTokenService Tokens(IDataProtectionProvider protection = null, IClock clock = null)
        => new(protection ?? new EphemeralDataProtectionProvider(), clock ?? new TestClock(DateTime.UtcNow));

    /// <summary>
    /// Returns a site URL generator on the given site settings, link generator and request.
    /// </summary>
    /// <param name="siteService">The site service, for the base URL.</param>
    /// <param name="linkGenerator">The link generator.</param>
    /// <param name="httpContext">The current request, or <see langword="null"/> for none.</param>
    public static SiteUrlGenerator Urls(ISiteService siteService, LinkGenerator linkGenerator, HttpContext httpContext = null)
        => new(siteService, new HttpContextAccessor { HttpContext = httpContext }, linkGenerator);
}
