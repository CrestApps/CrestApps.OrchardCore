using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.Workforce.Portal;

/// <summary>
/// The page a sign-out goes through on a device whose browser registered the My Work service worker: its script deletes
/// the My Work caches of the user who signed out in this tenant and unregisters the service worker of this tenant's My
/// Work pages, then goes on to where the sign-out was going. Nothing else the browser keeps for the host is touched:
/// other tenants on the same host, their service workers and push registrations, local storage and IndexedDB.
/// </summary>
internal static class MyWorkSignedOutEndpoints
{
    /// <summary>
    /// The path of the page, under the tenant.
    /// </summary>
    internal const string PagePath = "/my-work/signed-out";

    private const string ScriptPath = "/my-work/signed-out.js";

    // The caches the My Work service worker names for the user and tenant (my-work-pages-v2-{key}, my-work-assets-v2-{key})
    // and its scope's registration. The page goes on after both are done, or after three seconds if the browser takes
    // longer.
    private const string Script = """
        (() => {
            const me = document.currentScript;
            const scope = new URL(me.dataset.scope, location.origin).pathname.replace(/\/$/, '');
            const user = me.dataset.user;
            let done = false;
            const next = () => {
                if (!done) {
                    done = true;
                    location.replace(me.dataset.next);
                }
            };
            const work = [];
            if ('caches' in self) {
                work.push(caches.keys().then(keys => Promise.all(keys.filter(key => key.startsWith('my-work-') && key.endsWith('-' + user)).map(key => caches.delete(key)))));
            }
            if ('serviceWorker' in navigator) {
                work.push(navigator.serviceWorker.getRegistrations().then(registrations => Promise.all(registrations
                    .filter(registration => new URL(registration.scope).pathname.replace(/\/$/, '') === scope)
                    .map(registration => registration.unregister()))));
            }
            Promise.allSettled(work).then(next);
            setTimeout(next, 3000);
        })();
        """;

    /// <summary>
    /// Maps the page and its script.
    /// </summary>
    /// <param name="routes">The endpoint route builder.</param>
    public static IEndpointRouteBuilder MapMyWorkSignedOut(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        routes.MapGet(PagePath.TrimStart('/'), (HttpContext httpContext, string next, string u) => Page(httpContext, next, u)).AllowAnonymous();
        routes.MapGet(ScriptPath.TrimStart('/'), (HttpContext httpContext) => ScriptResult(httpContext)).AllowAnonymous();

        return routes;
    }

    /// <summary>
    /// Returns where a sign-out on a device that registered the My Work service worker goes instead of
    /// <paramref name="location"/>: this page, which goes on to <paramref name="location"/> when it is local.
    /// </summary>
    /// <param name="pathBase">The tenant's path base.</param>
    /// <param name="location">Where the sign-out was going.</param>
    /// <param name="userKey">The key the service worker named the user's caches with
    /// (<see cref="Controllers.MyWorkController.GetServiceWorkerUserKey(string, string)"/>).</param>
    internal static string GetPageUrl(PathString pathBase, string location, string userKey)
        => $"{pathBase}{PagePath}?next={Uri.EscapeDataString(LocalOrHome(pathBase, location))}&u={Uri.EscapeDataString(Controllers.MyWorkController.NormalizeServiceWorkerUserKey(userKey))}";

    internal static IResult Page(HttpContext httpContext, string next, string userKey)
    {
        var pathBase = httpContext.Request.PathBase;
        var html = HtmlEncoder.Default;

        httpContext.Response.Headers.CacheControl = "no-store";

        return Results.Content(
            "<!doctype html><html><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">" +
            "<meta name=\"robots\" content=\"noindex\"><title>My Work</title>" +
            $"<script src=\"{html.Encode($"{pathBase}{ScriptPath}")}\" data-scope=\"{html.Encode($"{pathBase}/Admin/my-work")}\" data-user=\"{html.Encode(Controllers.MyWorkController.NormalizeServiceWorkerUserKey(userKey))}\" data-next=\"{html.Encode(LocalOrHome(pathBase, next))}\"></script>" +
            $"<noscript><meta http-equiv=\"refresh\" content=\"0;url={html.Encode(LocalOrHome(pathBase, next))}\"></noscript>" +
            "</head><body></body></html>",
            "text/html; charset=utf-8");
    }

    internal static IResult ScriptResult(HttpContext httpContext)
    {
        httpContext.Response.Headers.CacheControl = "no-store";

        return Results.Content(Script, "text/javascript; charset=utf-8");
    }

    // A path on this site, never another host: an absolute address, a scheme-relative one (//host) and a backslash
    // (/\host, which browsers read as //host) go to the tenant's home.
    private static string LocalOrHome(PathString pathBase, string url)
    {
        if (!string.IsNullOrEmpty(url) &&
            url[0] == '/' &&
            (url.Length == 1 || (url[1] != '/' && url[1] != '\\')) &&
            !url.Any(char.IsControl))
        {
            return url;
        }

        return pathBase.HasValue ? pathBase.Value + "/" : "/";
    }
}
