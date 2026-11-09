using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Deployments;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Recipes;

/// <summary>
/// Imports Omnichannel dispositions carried by a recipe step, creating entries that do not exist and updating those
/// that do.
/// </summary>
internal sealed class OmnichannelDispositionStep : CatalogRecipeStep<OmnichannelDisposition>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelDispositionStep"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the dispositions.</param>
    public OmnichannelDispositionStep(INamedCatalogManager<OmnichannelDisposition> manager)
        : base(OmnichannelDeploymentSteps.Disposition, "Dispositions", manager)
    {
    }
}
