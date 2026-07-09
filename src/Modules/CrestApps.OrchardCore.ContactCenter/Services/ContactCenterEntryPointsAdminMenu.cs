using CrestApps.OrchardCore.ContactCenter.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Adds the inbound entry points entry to the Interaction Center admin navigation.
/// </summary>
public sealed class ContactCenterEntryPointsAdminMenu : AdminNavigationProvider
{
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterEntryPointsAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContactCenterEntryPointsAdminMenu(IStringLocalizer<ContactCenterEntryPointsAdminMenu> stringLocalizer)
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
                .Add(S["Inbound entry points"], S["Inbound entry points"].PrefixPosition(), entryPoints => entryPoints
                    .AddClass("contact-center-entry-points")
                    .Id("contactCenterEntryPoints")
                    .Action("Index", "EntryPoints", "CrestApps.OrchardCore.ContactCenter")
                    .Permission(ContactCenterPermissions.ManageQueues)
                    .LocalNav()),
                priority: 1);

        return ValueTask.CompletedTask;
    }
}
