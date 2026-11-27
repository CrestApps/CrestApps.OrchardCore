using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Deployments;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Recipes;

/// <summary>
/// Creates or updates messaging routing rules from a recipe, matched by <c>ItemId</c>.
/// </summary>
internal sealed class MessagingRoutingRulesStep : CatalogRecipeStep<MessagingRoutingRule>
{
    /// <summary>
    /// The recipe step's name.
    /// </summary>
    public const string StepKey = MessagingDeploymentSteps.RoutingRules;

    /// <summary>
    /// The name of the step's list of rules.
    /// </summary>
    public const string EntriesName = "Rules";

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingRoutingRulesStep"/> class.
    /// </summary>
    /// <param name="manager">The routing rules.</param>
    public MessagingRoutingRulesStep(ICatalogManager<MessagingRoutingRule> manager)
        : base(StepKey, EntriesName, manager)
    {
    }
}
