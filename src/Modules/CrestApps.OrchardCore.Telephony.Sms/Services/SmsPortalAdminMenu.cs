using CrestApps.OrchardCore.Telephony.Sms.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Telephony.Sms.Services;

/// <summary>
/// Adds the SMS portal and its number-route administration to the admin navigation.
/// </summary>
public sealed class SmsPortalAdminMenu : AdminNavigationProvider
{
    private readonly IStringLocalizer S;

    public SmsPortalAdminMenu(IStringLocalizer<SmsPortalAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["SMS Portal"], "15", portal => portal
                .AddClass("sms-portal")
                .Id("smsPortal")
                .Add(S["Inbox"], "1", inbox => inbox
                    .Action("Index", "SmsPortal", "CrestApps.OrchardCore.Telephony.Sms")
                    .Permission(TelephonySmsPermissions.UseSmsPortal)
                    .LocalNav())
                .Add(S["All conversations"], "2", all => all
                    .Action("All", "SmsPortal", "CrestApps.OrchardCore.Telephony.Sms")
                    .Permission(TelephonySmsPermissions.ViewAllConversations)
                    .LocalNav())
                .Add(S["Number routes"], "5", routes => routes
                    .Action("Index", "SmsNumberRoutes", "CrestApps.OrchardCore.Telephony.Sms")
                    .Permission(TelephonySmsPermissions.ManageSmsNumberRoutes)
                    .LocalNav())
                .Add(S["Broadcasts"], "6", broadcasts => broadcasts
                    .Action("Index", "SmsBroadcasts", "CrestApps.OrchardCore.Telephony.Sms")
                    .Permission(TelephonySmsPermissions.SendGroupSms)
                    .LocalNav())
                .Add(S["Templates"], "7", templates => templates
                    .Action("Index", "SmsTemplates", "CrestApps.OrchardCore.Telephony.Sms")
                    .Permission(TelephonySmsPermissions.ManageSmsNumberRoutes)
                    .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
