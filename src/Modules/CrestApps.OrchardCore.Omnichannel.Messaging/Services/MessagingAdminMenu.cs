using CrestApps.OrchardCore.Omnichannel.Messaging.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Services;

/// <summary>
/// Adds the messaging workspace (the inbox every channel feeds), its broadcasts and its templates to the admin
/// navigation.
/// </summary>
public sealed class MessagingAdminMenu : AdminNavigationProvider
{
    private readonly IStringLocalizer S;

    public MessagingAdminMenu(IStringLocalizer<MessagingAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Messaging"], S["Messaging"].PrefixPosition(), messaging => messaging
                .AddClass("messaging")
                .Id("messaging")
                .Add(S["Inbox"], "1", inbox => inbox
                    .Action("Index", "Admin", MessagingConstants.Feature.Workspace)
                    .Permission(MessagingPermissions.UseMessagingWorkspace)
                    .LocalNav())
                .Add(S["Broadcasts"], S["Broadcasts"].PrefixPosition(), broadcasts => broadcasts
                    .Action("Index", "Broadcasts", MessagingConstants.Feature.Workspace)
                    .Permission(MessagingPermissions.SendGroupMessages)
                    .LocalNav())
                .Add(S["Templates"], S["Templates"].PrefixPosition(), templates => templates
                    .Action("Index", "Templates", MessagingConstants.Feature.Workspace)
                    .Permission(MessagingPermissions.ManageMessaging)
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}
