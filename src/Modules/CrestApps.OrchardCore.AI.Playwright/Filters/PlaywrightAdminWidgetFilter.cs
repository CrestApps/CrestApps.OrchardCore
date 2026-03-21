using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Layout;

namespace CrestApps.OrchardCore.AI.Playwright.Filters;

/// <summary>
/// Injects the Playwright status bar into every admin page's Footer zone.
/// The bar is hidden by default and shown by JS once a Playwright session becomes active.
/// </summary>
public sealed class PlaywrightAdminWidgetFilter : IAsyncResultFilter
{
    private readonly ILayoutAccessor _layoutAccessor;
    private readonly IShapeFactory _shapeFactory;
    private readonly AdminOptions _adminOptions;

    public PlaywrightAdminWidgetFilter(
        ILayoutAccessor layoutAccessor,
        IShapeFactory shapeFactory,
        IOptions<AdminOptions> adminOptions)
    {
        _layoutAccessor = layoutAccessor;
        _shapeFactory = shapeFactory;
        _adminOptions = adminOptions.Value;
    }

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (!IsAdminPage(context) || context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            await next();
            return;
        }

        var shape = await _shapeFactory.CreateAsync("PlaywrightAdminWidget");

        var layout = await _layoutAccessor.GetLayoutAsync();

        // Place just after the AI chat widget (which is at "999").
        await layout.Zones["Footer"].AddAsync(shape, "1000");

        await next();
    }

    private bool IsAdminPage(ResultExecutingContext context)
    {
        if (context.Result is not (ViewResult or PageResult))
        {
            return false;
        }

        return context.HttpContext.Request.Path.StartsWithSegments(
            '/' + _adminOptions.AdminUrlPrefix,
            StringComparison.OrdinalIgnoreCase);
    }
}
