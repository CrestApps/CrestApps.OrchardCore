using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Taxation.Deployments.Steps;
using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Taxation.Deployments.Sources;

/// <summary>
/// Exports the stored tax types into the <c>TaxType</c> recipe step.
/// </summary>
internal sealed class TaxTypeDeploymentSource : NamedCatalogDeploymentSource<TaxTypeDeploymentStep, TaxType>
{
    public TaxTypeDeploymentSource(INamedCatalogManager<TaxType> manager)
        : base(TaxationDeploymentSteps.TaxTypeEntries, manager)
    {
    }
}
