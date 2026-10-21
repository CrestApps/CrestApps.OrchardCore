using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Taxation.Deployments.Steps;
using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Taxation.Deployments.Sources;

/// <summary>
/// Exports the stored tax categories into the <c>TaxCategory</c> recipe step.
/// </summary>
internal sealed class TaxCategoryDeploymentSource : NamedCatalogDeploymentSource<TaxCategoryDeploymentStep, TaxCategory>
{
    public TaxCategoryDeploymentSource(INamedCatalogManager<TaxCategory> manager)
        : base(TaxationDeploymentSteps.TaxCategoryEntries, manager)
    {
    }
}
