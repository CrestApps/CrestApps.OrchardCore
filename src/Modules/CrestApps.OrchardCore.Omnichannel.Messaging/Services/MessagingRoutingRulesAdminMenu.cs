using CrestApps.OrchardCore.Omnichannel.Messaging.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Services;

/// <summary>
/// Adds Routing Rules under the Messaging menu.
/// </summary>
/// <remarks>
/// Orchard merges every menu item with the same caption into the first one it meets and keeps that item's id and
/// classes. This provider can run before <see cref="MessagingAdminMenu"/>, so it names the Messaging item the same
/// way; otherwise the merged item has no id and the menu's icon (the <c>NavigationItemText-messaging.Id</c> shape) is lost.
/// </remarks>
internal sealed class MessagingRoutingRulesAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingRoutingRulesAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public MessagingRoutingRulesAdminMenu(IStringLocalizer<MessagingRoutingRulesAdminMenu> stringLocalizer)
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
                .Add(S["Routing Rules"], S["Routing Rules"].PrefixPosition(), rules => rules
                    .Action("Index", "RoutingRules", MessagingConstants.Feature.Workspace)
                    .Permission(MessagingPermissions.ManageRoutingRules)
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}
