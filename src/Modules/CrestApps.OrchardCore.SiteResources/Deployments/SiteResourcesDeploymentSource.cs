using System.Text.Json.Nodes;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.SiteResources.Models;

namespace CrestApps.OrchardCore.SiteResources.Deployments;

internal sealed class SiteResourcesDeploymentSource : CatalogDeploymentSource<SiteResourcesDeploymentStep, SiteResource>
{
    public SiteResourcesDeploymentSource(ICatalogManager<SiteResource> manager)
        : base("Resources", manager)
    {
    }

    // What is served is worked out again from the content on the receiving site.
    protected override JsonObject Export(SiteResource entry)
    {
        var json = base.Export(entry);

        json.Remove(nameof(SiteResource.ServedContent));
        json.Remove(nameof(SiteResource.Version));

        return json;
    }
}
