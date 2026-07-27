using CrestApps.OrchardCore.ContactCenter.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Adds the Contact Center preview maintenance procedure to the Contact Center admin navigation.
/// </summary>
public sealed class ContactCenterMaintenanceAdminMenu : AdminNavigationProvider
{
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterMaintenanceAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContactCenterMaintenanceAdminMenu(
        IStringLocalizer<ContactCenterMaintenanceAdminMenu> stringLocalizer)
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
                    .Add(S["Preview Maintenance"], S["Preview Maintenance"].PrefixPosition(), maintenance => maintenance
                    .AddClass("contact-center-preview-maintenance")
                    .Id("contactCenterPreviewMaintenance")
                    .Action("Index", "PreviewMaintenance", "CrestApps.OrchardCore.ContactCenter")
                    .Permission(ContactCenterPermissions.ManagePreviewData)
                        .LocalNav())),
                priority: 1);

        return ValueTask.CompletedTask;
    }
}
