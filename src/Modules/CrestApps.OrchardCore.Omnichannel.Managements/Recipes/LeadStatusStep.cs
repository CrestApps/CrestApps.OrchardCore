using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Deployments;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Recipes;

/// <summary>
/// Imports the lead statuses carried by a recipe step, creating entries that do not exist and updating those that do.
/// An entry is matched by its identifier, and then by its name, so replaying a plan never duplicates a seeded entry.
/// </summary>
internal sealed class LeadStatusStep : NamedCatalogRecipeStep<LeadStatus>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LeadStatusStep"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the lead statuses.</param>
    public LeadStatusStep(INamedCatalogManager<LeadStatus> manager)
        : base(OmnichannelDeploymentSteps.LeadStatus, "LeadStatuses", manager)
    {
    }

    /// <inheritdoc/>
    protected override bool MatchByName => true;
}
