using CrestApps.OrchardCore.Telephony.Sms.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Telephony.Sms.Services;

/// <summary>
/// Adds the SMS Portal settings entry to the admin navigation under Configuration → Settings.
/// </summary>
public sealed class SmsPortalSettingsAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _routeValues = new()
    {
        { "area", "OrchardCore.Settings" },
        { "groupId", TelephonySmsConstants.Settings.GroupId },
    };

    internal readonly IStringLocalizer S;

    public SmsPortalSettingsAdminMenu(IStringLocalizer<SmsPortalSettingsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Settings"], settings => settings
                .Add(S["SMS Portal"], S["SMS Portal"].PrefixPosition(), portal => portal
                    .Action("Index", "Admin", _routeValues)
                    .Permission(TelephonySmsPermissions.ManageSmsNumberRoutes)
                    .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
