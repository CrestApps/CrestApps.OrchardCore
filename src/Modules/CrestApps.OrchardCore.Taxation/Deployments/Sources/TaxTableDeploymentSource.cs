using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Taxation.Deployments.Steps;
using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Taxation.Deployments.Sources;

/// <summary>
/// Exports the stored tax tables into the <c>TaxTable</c> recipe step.
/// </summary>
internal sealed class TaxTableDeploymentSource : NamedCatalogDeploymentSource<TaxTableDeploymentStep, TaxTable>
{
    public TaxTableDeploymentSource(INamedCatalogManager<TaxTable> manager)
        : base(TaxationDeploymentSteps.TaxTableEntries, manager)
    {
    }
}
