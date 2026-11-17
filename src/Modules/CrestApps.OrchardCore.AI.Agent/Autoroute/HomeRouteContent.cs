using Microsoft.AspNetCore.Routing;
using OrchardCore.ContentManagement.Routing;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Agent.Autoroute;

/// <summary>
/// Reads the home route of the site settings: the route the site shows at its root address.
/// </summary>
internal static class HomeRouteContent
{
    /// <summary>
    /// Gets the ID of the content item the home route shows.
    /// </summary>
    /// <param name="site">The site settings.</param>
    /// <param name="options">The Autoroute options, which name the route value that holds the content item ID.</param>
    /// <returns>The content item ID, or <see langword="null"/> when the home route does not show a content item.</returns>
    public static string GetContentItemId(ISite site, AutorouteOptions options)
        => GetValue(site?.HomeRoute, options?.ContentItemIdKey);

    /// <summary>
    /// Gets the path of the item inside the content item the home route shows, when the home page is an item contained
    /// in another content item.
    /// </summary>
    /// <param name="site">The site settings.</param>
    /// <param name="options">The Autoroute options, which name the route value that holds the path.</param>
    /// <returns>The path, or <see langword="null"/> when the home page is the content item itself.</returns>
    public static string GetJsonPath(ISite site, AutorouteOptions options)
        => GetValue(site?.HomeRoute, options?.JsonPathKey);

    /// <summary>
    /// Gets the route the home route points to, for a result.
    /// </summary>
    /// <param name="site">The site settings.</param>
    /// <returns>The area, controller and action of the home route, or <see langword="null"/> when the site has no home route.</returns>
    public static object Describe(ISite site)
    {
        var homeRoute = site?.HomeRoute;

        if (homeRoute is null || homeRoute.Count == 0)
        {
            return null;
        }

        return new
        {
            area = GetValue(homeRoute, "area"),
            controller = GetValue(homeRoute, "controller"),
            action = GetValue(homeRoute, "action"),
        };
    }

    private static string GetValue(RouteValueDictionary homeRoute, string key)
    {
        if (homeRoute is null || string.IsNullOrEmpty(key) || !homeRoute.TryGetValue(key, out var value))
        {
            return null;
        }

        var text = value?.ToString();

        return string.IsNullOrEmpty(text)
            ? null
            : text;
    }
}
