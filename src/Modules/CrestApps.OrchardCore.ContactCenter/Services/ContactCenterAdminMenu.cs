using CrestApps.OrchardCore.ContactCenter.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Adds the Contact Center entries to the admin navigation.
/// </summary>
public sealed class ContactCenterAdminMenu : AdminNavigationProvider
{
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContactCenterAdminMenu(IStringLocalizer<ContactCenterAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Contact Center"], "6", contactCenter => contactCenter
                .AddClass("contact-center")
                .Id("contactCenter")
                .Add(S["Agent Workspace"], S["Agent Workspace"].PrefixPosition(), workspace => workspace
                    .Action("Index", "AgentWorkspace", "CrestApps.OrchardCore.ContactCenter")
                    .Permission(ContactCenterPermissions.SignIntoQueues)
                    .LocalNav())
                .Add(S["Queues"], S["Queues"].PrefixPosition(), queues => queues
                    .Action("Index", "Queues", "CrestApps.OrchardCore.ContactCenter")
                    .Permission(ContactCenterPermissions.ManageQueues)
                    .LocalNav())
                .Add(S["Dialer Profiles"], S["Dialer Profiles"].PrefixPosition(), dialer => dialer
                    .Action("Index", "DialerProfiles", "CrestApps.OrchardCore.ContactCenter")
                    .Permission(ContactCenterPermissions.ManageDialer)
                    .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
