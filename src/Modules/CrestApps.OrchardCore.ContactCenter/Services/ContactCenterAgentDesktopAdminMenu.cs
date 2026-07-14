using CrestApps.OrchardCore.ContactCenter.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Adds the agent workspace to the Contact Center admin navigation.
/// </summary>
public sealed class ContactCenterAgentDesktopAdminMenu : AdminNavigationProvider
{
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterAgentDesktopAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContactCenterAgentDesktopAdminMenu(IStringLocalizer<ContactCenterAgentDesktopAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Contact Center"], "80", contactCenter => contactCenter
                .AddClass("contact-center")
                .Id("contactCenter")
                .Add(S["My workspace"], "-2", workspace => workspace
                    .AddClass("contact-center-workspace")
                    .Id("contactCenterWorkspace")
                    .Action("Index", "AgentWorkspace", "CrestApps.OrchardCore.ContactCenter")
                    .Permission(ContactCenterPermissions.SignIntoQueues)
                    .LocalNav()
                ),
                priority: 2);

        return ValueTask.CompletedTask;
    }
}
