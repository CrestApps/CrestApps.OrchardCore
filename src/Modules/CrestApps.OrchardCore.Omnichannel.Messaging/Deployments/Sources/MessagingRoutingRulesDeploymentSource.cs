using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Deployments.Steps;
using CrestApps.OrchardCore.Omnichannel.Messaging.Recipes;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Deployments.Sources;

/// <summary>
/// Exports every messaging routing rule, in the order the rules are tried.
/// </summary>
internal sealed class MessagingRoutingRulesDeploymentSource : CatalogDeploymentSource<MessagingRoutingRulesDeploymentStep, MessagingRoutingRule>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingRoutingRulesDeploymentSource"/> class.
    /// </summary>
    /// <param name="manager">The routing rules.</param>
    public MessagingRoutingRulesDeploymentSource(ICatalogManager<MessagingRoutingRule> manager)
        : base(MessagingRoutingRulesStep.EntriesName, manager)
    {
    }

    /// <inheritdoc/>
    protected override IEnumerable<MessagingRoutingRule> Order(IEnumerable<MessagingRoutingRule> entries)
        => entries
            .OrderBy(entry => entry.Order)
            .ThenBy(entry => entry.DisplayText, StringComparer.OrdinalIgnoreCase);
}
