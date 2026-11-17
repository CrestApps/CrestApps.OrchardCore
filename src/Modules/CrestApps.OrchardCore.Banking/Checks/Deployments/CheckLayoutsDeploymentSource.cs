using System.Text.Json.Nodes;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Banking.Checks.Models;
using CrestApps.OrchardCore.Banking.Checks.Recipes;
using CrestApps.OrchardCore.Core.Deployments;

namespace CrestApps.OrchardCore.Banking.Checks.Deployments;

internal sealed class CheckLayoutsDeploymentSource : NamedCatalogDeploymentSource<CheckLayoutsDeploymentStep, CheckLayout>
{
    public CheckLayoutsDeploymentSource(INamedCatalogManager<CheckLayout> manager)
        : base(CheckLayoutsRecipeStep.EntriesName, manager)
    {
    }

    // The files stay on this site, so the importing site does not claim it has them.
    protected override JsonObject Export(CheckLayout entry)
    {
        var json = base.Export(entry);

        foreach (var key in json.Select(property => property.Key).Where(key => key.StartsWith("Has", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            json.Remove(key);
        }

        return json;
    }
}
