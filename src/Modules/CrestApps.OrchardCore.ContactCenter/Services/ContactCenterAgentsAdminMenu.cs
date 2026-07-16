using CrestApps.OrchardCore.ContactCenter.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Adds the Contact Center agent configuration entries to the Contact Center admin navigation.
/// </summary>
public sealed class ContactCenterAgentsAdminMenu : AdminNavigationProvider
{
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterAgentsAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContactCenterAgentsAdminMenu(IStringLocalizer<ContactCenterAgentsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Interaction Center"], "80", interactionCenter => interactionCenter
                .AddClass("interaction-center")
                .Id("interactionCenter")
                .Add(S["Management"], "100", management => management
                    .AddClass("interaction-center-management")
                    .Id("interactionCenterManagement")
                    .Add(S["Agent states"], S["Agent states"].PrefixPosition(), agentStates => agentStates
                    .AddClass("contact-center-agent-states")
                    .Id("contactCenterAgentStates")
                    .Action("Index", "AgentStateReasonCodes", "CrestApps.OrchardCore.ContactCenter")
                    .Permission(ContactCenterPermissions.ManageAgents)
                        .LocalNav())),
                priority: 1);

        return ValueTask.CompletedTask;
    }
}
