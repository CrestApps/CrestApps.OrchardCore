using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Zones;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Reads the admin menu the signed-in caller sees, the same walk the admin quick navigation palette does: the
/// navigation manager builds the menu for the caller's request and leaves out what the caller may not open, so the
/// result belongs to that caller only and is never cached.
/// </summary>
internal static class AdminMenuPages
{
    /// <summary>
    /// Builds the admin menu of the caller and returns every page in it that has a link.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="httpContext">The request of the caller.</param>
    /// <returns>The pages, in menu order.</returns>
    public static async Task<List<AdminPage>> GetPagesAsync(IServiceProvider services, HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(httpContext);

        var navigationManager = services.GetRequiredService<INavigationManager>();
        var actionContext = new ActionContext(httpContext, httpContext.GetRouteData() ?? new RouteData(), new ActionDescriptor());

        var menu = await navigationManager.BuildMenuAsync(NavigationConstants.AdminId, actionContext);
        var pages = new List<AdminPage>();

        AddPages(menu ?? [], [], pages);

        return pages;
    }

    private static void AddPages(IEnumerable<MenuItem> items, string[] path, List<AdminPage> pages)
    {
        foreach (var item in items.OrderBy(item => item.Position, FlatPositionComparer.Instance))
        {
            var title = item.Text?.Value;

            if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrEmpty(item.Href) && item.Href != "#" &&
                !pages.Exists(page => page.Title == title && page.Href == item.Href))
            {
                pages.Add(new AdminPage
                {
                    Title = title,
                    Path = path,
                    Href = item.Href,
                    RouteValues = item.RouteValues,
                });
            }

            AddPages(item.Items ?? [], string.IsNullOrWhiteSpace(title) ? path : [.. path, title], pages);
        }
    }
}

/// <summary>
/// One page of the admin menu.
/// </summary>
internal sealed class AdminPage
{
    /// <summary>
    /// Gets or sets the title of the menu item, in the caller's language.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the titles of the menu items above it.
    /// </summary>
    public string[] Path { get; set; } = [];

    /// <summary>
    /// Gets or sets the link of the page.
    /// </summary>
    public string Href { get; set; }

    /// <summary>
    /// Gets or sets the route values of the page, when the menu item has them.
    /// </summary>
    public RouteValueDictionary RouteValues { get; set; }

    /// <summary>
    /// Gets the menu path with the title, such as <c>Configuration &gt; Settings &gt; Admin</c>.
    /// </summary>
    public string MenuPath
        => string.Join(" > ", Path.Append(Title));
}
