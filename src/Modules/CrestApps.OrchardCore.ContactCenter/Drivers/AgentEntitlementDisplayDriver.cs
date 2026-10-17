using CrestApps.OrchardCore.ContactCenter.Core.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.ContactCenter.Drivers;

/// <summary>
/// Renders an agent's row on the agent entitlements list: who the agent is and how many queues and campaigns they
/// are granted.
/// </summary>
internal sealed class AgentEntitlementDisplayDriver : DisplayDriver<AgentProfile>
{
    /// <inheritdoc/>
    public override Task<IDisplayResult> DisplayAsync(AgentProfile agent, BuildDisplayContext context)
    {
        // An agent profile is only ever listed here; any other display of it is left to the features that own it.
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        // The row's classes and attributes are rendered on its row element, in both admin list layouts.
        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = (agent.UserName ?? agent.DisplayName)?.ToLowerInvariant() ?? string.Empty;
        }

        return CombineAsync(
            View("AgentProfile_Fields_SummaryAdmin", agent)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("AgentProfile_DefaultTags_SummaryAdmin", agent)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Entitlements:5"),
            View("AgentProfile_Buttons_SummaryAdmin", agent).Location("Actions:5")
        );
    }
}
