using CrestApps.OrchardCore.ContactCenter.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Adds the Contact Center dialer entries to the Contact Center admin navigation.
/// </summary>
public sealed class ContactCenterDialerAdminMenu : AdminNavigationProvider
{
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterDialerAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContactCenterDialerAdminMenu(IStringLocalizer<ContactCenterDialerAdminMenu> stringLocalizer)
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
                .Add(S["Dialer Profiles"], S["Dialer Profiles"].PrefixPosition(), dialer => dialer
                    .AddClass("dialer-profiles")
                    .Id("dialerProfiles")
                    .Action("Index", "DialerProfiles", "CrestApps.OrchardCore.ContactCenter")
                    .Permission(ContactCenterPermissions.ManageDialer)
                    .LocalNav()
                ),
                priority: 1);

        return ValueTask.CompletedTask;
    }
}
