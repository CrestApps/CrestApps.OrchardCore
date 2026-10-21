using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Taxation.Deployments.Steps;
using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Taxation.Deployments.Sources;

/// <summary>
/// Exports the stored tax rules into the <c>TaxRule</c> recipe step.
/// </summary>
internal sealed class TaxRuleDeploymentSource : NamedCatalogDeploymentSource<TaxRuleDeploymentStep, TaxRule>
{
    public TaxRuleDeploymentSource(INamedCatalogManager<TaxRule> manager)
        : base(TaxationDeploymentSteps.TaxRuleEntries, manager)
    {
    }
}
