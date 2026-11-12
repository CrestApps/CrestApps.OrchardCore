using Microsoft.AspNetCore.Http;
using OrchardCore.Users;
using OrchardCore.Users.Events;

namespace CrestApps.OrchardCore.Workforce.Portal;

/// <summary>
/// Clears what the My Work service worker kept on a device when its user signs out. The worker keeps the pages that work
/// offline, so on a shared tablet that goes offline after a sign-out the next person would otherwise be shown the last
/// worker's page. The sign-out response tells the browser to drop the site's caches and storage, which also removes the
/// service worker, on a device that registered it.
/// </summary>
internal sealed class MyWorkSignOut : LogoutFormEventBase
{
    /// <summary>
    /// The cookie that marks a device whose browser registered the My Work service worker.
    /// </summary>
    internal const string DeviceCookieName = "crestapps-my-work-device";

    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="MyWorkSignOut"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The current request, whose response is the sign-out.</param>
    public MyWorkSignOut(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
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

        // Only a device that registered the worker: the header clears every cache and all storage of the site, the
        // admin's remembered layout included.
        if (httpContext is not null && httpContext.Request.Cookies.ContainsKey(DeviceCookieName))
        {
            httpContext.Response.Headers["Clear-Site-Data"] = "\"cache\", \"storage\"";
        }

        return Task.CompletedTask;
    }
}
