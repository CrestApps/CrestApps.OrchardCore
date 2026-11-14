using System.Security.Claims;
using CrestApps.OrchardCore.Workforce.Controllers;
using Microsoft.AspNetCore.Http;
using OrchardCore.Environment.Shell;
using OrchardCore.Users;
using OrchardCore.Users.Events;

namespace CrestApps.OrchardCore.Workforce.Portal;

/// <summary>
/// Clears what the My Work service worker kept on a device when its user signs out. The worker keeps the pages that work
/// offline, so on a shared tablet that goes offline after a sign-out the next person would otherwise be shown the last
/// worker's page. On a device whose browser registered the worker, the sign-out goes through a page
/// (<see cref="MyWorkSignedOutEndpoints"/>) whose script deletes the My Work caches and unregisters this tenant's My Work
/// service worker, then goes on to where the sign-out was going. It never sends <c>Clear-Site-Data</c>, which clears the
/// whole origin: with tenants on paths of one host, every tenant's storage, service workers and push registrations.
/// </summary>
internal sealed class MyWorkSignOut : LogoutFormEventBase
{
    /// <summary>
    /// The cookie that marks a device whose browser registered the My Work service worker.
    /// </summary>
    internal const string DeviceCookieName = "crestapps-my-work-device";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ShellSettings _shellSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="MyWorkSignOut"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The current request, whose response is the sign-out.</param>
    /// <param name="shellSettings">The tenant, which the service worker's cache names are keyed with.</param>
    public MyWorkSignOut(IHttpContextAccessor httpContextAccessor, ShellSettings shellSettings)
    {
        _httpContextAccessor = httpContextAccessor;
        _shellSettings = shellSettings;
    }

    /// <summary>
    /// Marks the device as one whose browser registered the My Work service worker. Called when the worker's script is
    /// served, so the sign-out knows to clear it.
    /// </summary>
    /// <param name="request">The request for the service worker's script.</param>
    /// <param name="response">Its response.</param>
    internal static void RememberDevice(HttpRequest request, HttpResponse response)
        => response.Cookies.Append(DeviceCookieName, "1", new CookieOptions
        {
            Path = request.PathBase.HasValue ? request.PathBase.Value : "/",
            HttpOnly = true,
            Secure = request.IsHttps,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            MaxAge = TimeSpan.FromDays(365),
        });

    /// <inheritdoc/>
    public override Task LoggedOutAsync(IUser user, CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        // Only a device that registered the worker. The sign-out answers with its redirect after this runs, so the
        // redirect is pointed through the page as the response starts.
        if (httpContext is not null && httpContext.Request.Cookies.ContainsKey(DeviceCookieName))
        {
            // The service worker named the user's caches with this key (_MyWorkAssets): only those are deleted.
            var userKey = MyWorkController.GetServiceWorkerUserKey(httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier), _shellSettings.Name);

            httpContext.Response.OnStarting(() =>
            {
                ThroughThePage(httpContext, userKey);

                return Task.CompletedTask;
            });
        }

        return Task.CompletedTask;
    }

    private static void ThroughThePage(HttpContext httpContext, string userKey)
    {
        var response = httpContext.Response;

        if (response.StatusCode is < 300 or > 399 || string.IsNullOrEmpty(response.Headers.Location))
        {
            return;
        }

        response.Headers.Location = MyWorkSignedOutEndpoints.GetPageUrl(httpContext.Request.PathBase, response.Headers.Location.ToString(), userKey);
    }
}
