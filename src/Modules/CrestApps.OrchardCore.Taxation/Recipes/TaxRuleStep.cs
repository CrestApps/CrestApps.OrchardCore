using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Taxation.Deployments;
using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Taxation.Recipes;

/// <summary>
/// Imports the tax rules carried by the <c>TaxRule</c> recipe step, creating entries that do not exist and updating those that do.
/// </summary>
internal sealed class TaxRuleStep : NamedCatalogRecipeStep<TaxRule>
{
    public TaxRuleStep(INamedCatalogManager<TaxRule> manager)
        : base(TaxationDeploymentSteps.TaxRule, TaxationDeploymentSteps.TaxRuleEntries, manager)
    {
    }
}
