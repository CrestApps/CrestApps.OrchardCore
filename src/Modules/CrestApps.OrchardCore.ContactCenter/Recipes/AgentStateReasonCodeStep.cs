using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Deployments;
using CrestApps.OrchardCore.Core.Recipes;

namespace CrestApps.OrchardCore.ContactCenter.Recipes;

/// <summary>
/// Imports Contact Center agent state reason codes carried by a recipe step, creating entries that do not exist and updating those that
/// do.
/// </summary>
internal sealed class AgentStateReasonCodeStep : CatalogRecipeStep<AgentStateReasonCode>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AgentStateReasonCodeStep"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the agent state reason codes.</param>
    public AgentStateReasonCodeStep(IAgentStateReasonCodeManager manager)
        : base(ContactCenterDeploymentSteps.AgentStateReasonCode, "ReasonCodes", manager)
    {
    }
}
