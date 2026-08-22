using CrestApps.OrchardCore.Omnichannel.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Adds the Channel Endpoints administration entry. It lives in the dependency-only Channel Endpoints feature
/// so a feature that reuses channel endpoints (such as the SMS portal) gets this one screen without the full
/// Omnichannel management navigation. It merges into the same Interaction Center → Management path used by the
/// full management menu when both are enabled.
/// </summary>
internal sealed class ChannelEndpointsAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    public ChannelEndpointsAdminMenu(IStringLocalizer<ChannelEndpointsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Interaction Center"], "80", interactionCenter => interactionCenter
                .AddClass("interaction-center")
                .Id("interactionCenter")
                .Add(S["Management"], S["Management"].PrefixPosition(), management => management
                    .AddClass("interaction-center-management")
                    .Id("interactionCenterManagement")
                    .Add(S["Channel Endpoints"], S["Channel Endpoints"].PrefixPosition(), endpoints => endpoints
                        .AddClass("channel-endpoints")
                        .Id("channelEndpoints")
                        .Action("Index", "ChannelEndpoints", "CrestApps.OrchardCore.Omnichannel.Managements")
                        .Permission(OmnichannelConstants.Permissions.ManageChannelEndpoints)
                        .LocalNav())),
                priority: 1);

        return ValueTask.CompletedTask;
    }
}
