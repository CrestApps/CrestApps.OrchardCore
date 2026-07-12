using CrestApps.OrchardCore.ContactCenter.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Adds the real-time agent and supervisor surfaces to the Interaction Center admin navigation: the agent
/// desktop where agents work and the supervisor dashboard where managers monitor operations.
/// </summary>
public sealed class ContactCenterRealTimeAdminMenu : AdminNavigationProvider
{
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterRealTimeAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContactCenterRealTimeAdminMenu(IStringLocalizer<ContactCenterRealTimeAdminMenu> stringLocalizer)
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
                .Add(S["My workspace"], "-2", workspace => workspace
                    .AddClass("contact-center-workspace")
                    .Id("contactCenterWorkspace")
                    .Action("Index", "AgentWorkspace", "CrestApps.OrchardCore.ContactCenter")
                    .Permission(ContactCenterPermissions.SignIntoQueues)
                    .LocalNav()
                )
                .Add(S["Live dashboard"], S["Live dashboard"].PrefixPosition("2"), dashboard => dashboard
                    .AddClass("contact-center-dashboard")
                    .Id("contactCenterDashboard")
                    .Action("Index", "SupervisorDashboard", "CrestApps.OrchardCore.ContactCenter")
                    .Permission(ContactCenterPermissions.MonitorContactCenter)
                    .LocalNav()
                ),
                priority: 2);

        return ValueTask.CompletedTask;
    }
}
