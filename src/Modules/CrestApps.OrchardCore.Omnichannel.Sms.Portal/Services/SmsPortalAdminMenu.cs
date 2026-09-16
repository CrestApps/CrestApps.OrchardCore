using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Services;

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
            .Add(S["SMS Portal"], S["SMS Portal"].PrefixPosition(), portal => portal
                .AddClass("sms-portal")
                .Id("smsPortal")
                .Add(S["Conversations"], "1", conversations => conversations
                    .Action("Index", "Admin", "CrestApps.OrchardCore.Omnichannel.Sms.Portal")
                    .Permission(SmsPortalPermissions.UseSmsPortal)
                    .LocalNav())
                .Add(S["Broadcasts"], S["Broadcasts"].PrefixPosition(), broadcasts => broadcasts
                    .Action("Index", "SmsBroadcasts", "CrestApps.OrchardCore.Omnichannel.Sms.Portal")
                    .Permission(SmsPortalPermissions.SendGroupSms)
                    .LocalNav())
                .Add(S["Templates"], S["Templates"].PrefixPosition(), templates => templates
                    .Action("Index", "SmsTemplates", "CrestApps.OrchardCore.Omnichannel.Sms.Portal")
                    .Permission(SmsPortalPermissions.ManageSmsNumberRoutes)
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}
