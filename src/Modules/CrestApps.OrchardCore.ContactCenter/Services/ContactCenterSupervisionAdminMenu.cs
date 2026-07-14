using CrestApps.OrchardCore.ContactCenter.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Adds the real-time supervisor dashboard to the Contact Center admin navigation.
/// </summary>
public sealed class ContactCenterSupervisionAdminMenu : AdminNavigationProvider
{
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterSupervisionAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContactCenterSupervisionAdminMenu(IStringLocalizer<ContactCenterSupervisionAdminMenu> stringLocalizer)
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
