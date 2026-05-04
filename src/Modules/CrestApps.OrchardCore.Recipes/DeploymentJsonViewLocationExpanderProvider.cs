using Microsoft.AspNetCore.Mvc.Razor;
using OrchardCore.Mvc.LocationExpander;

namespace CrestApps.OrchardCore.Recipes;

internal sealed class DeploymentJsonViewLocationExpander : IViewLocationExpanderProvider
{
    // Priority determines which expander runs first.
    // Higher priority runs first; set 0 or 10 is fine here.
    // Called by Razor; we don't need any custom values here

    // Return your view location before the default ones
    public int Priority => 0;

    /// <summary>
    /// Performs the populate values operation.
    /// </summary>
    /// <param name="context">The context.</param>
    public void PopulateValues(ViewLocationExpanderContext context)
    {
    }

    /// <summary>
    /// Performs the expand view locations operation.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="viewLocations">The view locations.</param>
    public IEnumerable<string> ExpandViewLocations(ViewLocationExpanderContext context, IEnumerable<string> viewLocations)
    {
        // Only override the Import/Json view

        if (context.AreaName == "OrchardCore.Deployment" && context.ControllerName == "Import" && context.ViewName == "Json")
        {
            // Return your module path first

            yield return "/Areas/CrestApps.OrchardCore.Recipes/Views/Import/Json.cshtml";
        }

        // Then let the normal locations still be searched

        foreach (var location in viewLocations)
        {
            yield return location;
        }
    }
}
