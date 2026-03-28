using Microsoft.AspNetCore.Mvc.Razor;
using OrchardCore.Mvc.LocationExpander;

namespace CrestApps.OrchardCore.Recipes;

internal sealed class DeploymentJsonViewLocationExpander : IViewLocationExpanderProvider
{
    // Priority determines which expander runs first.
    // Higher priority runs first; set 0 or 10 is fine here.
    public int Priority => 0;

    // Called by Razor; we don't need any custom values here
    public void PopulateValues(ViewLocationExpanderContext context)
    {
    }

    // Return your view location before the default ones
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
