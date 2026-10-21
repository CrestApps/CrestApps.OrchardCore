using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Taxation.Deployments.Steps;
using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Taxation.Deployments.Sources;

/// <summary>
/// Exports the stored tax jurisdictions into the <c>TaxJurisdiction</c> recipe step.
/// </summary>
internal sealed class TaxJurisdictionDeploymentSource : NamedCatalogDeploymentSource<TaxJurisdictionDeploymentStep, TaxJurisdiction>
{
    public TaxJurisdictionDeploymentSource(INamedCatalogManager<TaxJurisdiction> manager)
        : base(TaxationDeploymentSteps.TaxJurisdictionEntries, manager)
    {
    }
}
