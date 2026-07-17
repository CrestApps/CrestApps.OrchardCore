using CrestApps.OrchardCore.ContactCenter.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Adds the Contact Center settings entry to the admin navigation under Settings.
/// </summary>
public sealed class ContactCenterSettingsAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _routeValues = new()
    {
        { "area", "OrchardCore.Settings" },
        { "groupId", ContactCenterConstants.Settings.GroupId },
    };

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterSettingsAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContactCenterSettingsAdminMenu(IStringLocalizer<ContactCenterSettingsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Settings"], settings => settings
                .Add(S["Contact Center"], S["Contact Center"].PrefixPosition(), contactCenter => contactCenter
                    .Action("Index", "Admin", _routeValues)
                    .Permission(ContactCenterPermissions.ManageContactCenter)
                    .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
