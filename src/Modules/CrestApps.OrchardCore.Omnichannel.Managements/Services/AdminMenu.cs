using CrestApps.OrchardCore.Omnichannel.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

internal sealed class AdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    public AdminMenu(IStringLocalizer<AdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Interaction Center"], S["Interaction Center"].PrefixPosition(), interactionCenter => interactionCenter
                .AddClass("interaction-center")
                .Id("interactionCenter")
                .Add(S["Activities"], "1", activities => activities
                    .AddClass("activities")
                    .Id("activities")
                    .Action("Activities", "Activities", "CrestApps.OrchardCore.Omnichannel.Managements")
                    .Permission(OmnichannelConstants.Permissions.ListActivities)
                    .LocalNav()
                )
                .Add(S["Activity Batches"], S["Activity Batches"].PrefixPosition(), dispositions => dispositions
                    .AddClass("activity-batches")
                    .Id("activityBatches")
                    .Action("Index", "ActivityBatches", "CrestApps.OrchardCore.Omnichannel.Managements")
                    .Permission(OmnichannelConstants.Permissions.ManageChannelEndpoints)
                    .LocalNav()
                )
                .Add(S["Campaigns"], S["Campaigns"].PrefixPosition(), campaigns => campaigns
                    .AddClass("Campaigns")
                    .Id("Campaigns")
                    .Action("Index", "Campaigns", "CrestApps.OrchardCore.Omnichannel.Managements")
                    .Permission(OmnichannelConstants.Permissions.ManageCampaigns)
                    .LocalNav()
                )
                .Add(S["Dispositions"], S["Dispositions"].PrefixPosition(), dispositions => dispositions
                    .AddClass("dispositions")
                    .Id("dispositions")
                    .Action("Index", "Dispositions", "CrestApps.OrchardCore.Omnichannel.Managements")
                    .Permission(OmnichannelConstants.Permissions.ManageDispositions)
                    .LocalNav()
                )
                .Add(S["Channel Endpoints"], S["Channel Endpoints"].PrefixPosition(), dispositions => dispositions
                    .AddClass("channel-endpoints")
                    .Id("channelEndpoints")
                    .Action("Index", "ChannelEndpoints", "CrestApps.OrchardCore.Omnichannel.Managements")
                    .Permission(OmnichannelConstants.Permissions.ManageChannelEndpoints)
                    .LocalNav()
                )

            , priority: 1);

        return ValueTask.CompletedTask;
    }
}

