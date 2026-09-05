using CrestApps.OrchardCore.Sms.Workspace.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Sms.Workspace.Services;

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
            .Add(S["SMS Workspace"], S["SMS Workspace"].PrefixPosition(), portal => portal
                .AddClass("sms-workspace")
                .Id("smsWorkspace")
                .Add(S["Conversations"], "1", conversations => conversations
                    .Action("Index", "Admin", "CrestApps.OrchardCore.Sms.Workspace")
                    .Permission(SmsWorkspacePermissions.UseSmsPortal)
                    .LocalNav())
                .Add(S["Broadcasts"], S["Broadcasts"].PrefixPosition(), broadcasts => broadcasts
                    .Action("Index", "SmsBroadcasts", "CrestApps.OrchardCore.Sms.Workspace")
                    .Permission(SmsWorkspacePermissions.SendGroupSms)
                    .LocalNav())
                .Add(S["Templates"], S["Templates"].PrefixPosition(), templates => templates
                    .Action("Index", "SmsTemplates", "CrestApps.OrchardCore.Sms.Workspace")
                    .Permission(SmsWorkspacePermissions.ManageSmsNumberRoutes)
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}
