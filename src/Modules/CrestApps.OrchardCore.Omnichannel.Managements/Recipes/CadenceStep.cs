using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Deployments;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Recipes;

/// <summary>
/// Imports Omnichannel re-engagement cadences carried by a recipe step, creating entries that do not exist and
/// updating those that do.
/// </summary>
internal sealed class CadenceStep : CatalogRecipeStep<Cadence>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CadenceStep"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the cadences.</param>
    public CadenceStep(ICatalogManager<Cadence> manager)
        : base(OmnichannelDeploymentSteps.Cadence, "Cadences", manager)
    {
    }
}
