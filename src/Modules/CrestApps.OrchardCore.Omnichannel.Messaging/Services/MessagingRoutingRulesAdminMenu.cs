using CrestApps.OrchardCore.Omnichannel.Messaging.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Services;

/// <summary>
/// Adds Routing Rules under the Messaging menu.
/// </summary>
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
                .Add(S["Routing Rules"], S["Routing Rules"].PrefixPosition(), rules => rules
                    .Action("Index", "RoutingRules", MessagingConstants.Feature.Workspace)
                    .Permission(MessagingPermissions.ManageRoutingRules)
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}
