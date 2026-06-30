using CrestApps.OrchardCore.ContactCenter.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Adds the Contact Center management entries to the Interaction Center admin navigation.
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
            .Add(S["Interaction Center"], "80", interactionCenter => interactionCenter
                .AddClass("interaction-center")
                .Id("interactionCenter")
                .Add(S["Queues"], S["Queues"].PrefixPosition(), queues => queues
                    .AddClass("contact-center-queues")
                    .Id("contactCenterQueues")
                    .Action("Index", "Queues", "CrestApps.OrchardCore.ContactCenter")
                    .Permission(ContactCenterPermissions.ManageQueues)
                    .LocalNav())
                .Add(S["Skills"], S["Skills"].PrefixPosition(), skills => skills
                    .AddClass("contact-center-skills")
                    .Id("contactCenterSkills")
                    .Action("Index", "Skills", "CrestApps.OrchardCore.ContactCenter")
                    .Permission(ContactCenterPermissions.ManageSkills)
                    .LocalNav()),
                priority: 1);

        return ValueTask.CompletedTask;
    }
}
