using CrestApps.OrchardCore.Omnichannel.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

internal sealed class AdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AdminMenu(IStringLocalizer<AdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Interaction Center"], "80", interactionCenter => interactionCenter
                .AddClass("interaction-center")
                .Id("interactionCenter")
                .Add(S["Activities"], "-1", activities => activities
                    .AddClass("activities")
                    .Id("activities")
                    .Action("Activities", "Activities", "CrestApps.OrchardCore.Omnichannel.Managements")
                    .Permission(OmnichannelConstants.Permissions.ListActivities)
                    .LocalNav()
                )
                .Add(S["Management"], "100", management => management
                    .AddClass("interaction-center-management")
                    .Id("interactionCenterManagement")
                    .Add(S["Manage Activities"], S["Manage Activities"].PrefixPosition("3"), manageActivities => manageActivities
                        .AddClass("manage-activities")
                        .Id("manageActivities")
                        .Action("ManageActivities", "Activities", "CrestApps.OrchardCore.Omnichannel.Managements")
                        .Permission(OmnichannelConstants.Permissions.ManageActivities)
                        .LocalNav())
                    .Add(S["Load Inventory"], S["Load Inventory"].PrefixPosition(), inventory => inventory
                        .AddClass("activity-batches")
                        .Id("activityBatches")
                        .Action("Index", "ActivityBatches", "CrestApps.OrchardCore.Omnichannel.Managements")
                        .Permission(OmnichannelConstants.Permissions.ManageActivityBatches)
                        .LocalNav())
                    .Add(S["Subject Flows"], S["Subject Flows"].PrefixPosition(), subjectFlows => subjectFlows
                        .AddClass("subject-flows")
                        .Id("subjectFlows")
                        .Action("Index", "SubjectFlows", "CrestApps.OrchardCore.Omnichannel.Managements")
                        .Permission(OmnichannelConstants.Permissions.ManageSubjectFlows)
                        .LocalNav())
                    .Add(S["Campaigns"], S["Campaigns"].PrefixPosition(), campaigns => campaigns
                        .AddClass("campaigns")
                        .Id("campaigns")
                        .Action("Index", "Campaigns", "CrestApps.OrchardCore.Omnichannel.Managements")
                        .Permission(OmnichannelConstants.Permissions.ManageCampaigns)
                        .LocalNav())
                    .Add(S["Campaign Groups"], S["Campaign Groups"].PrefixPosition(), campaignGroups => campaignGroups
                        .AddClass("campaign-groups")
                        .Id("campaignGroups")
                        .Action("Index", "CampaignGroups", "CrestApps.OrchardCore.Omnichannel.Managements")
                        .Permission(OmnichannelConstants.Permissions.ManageCampaignGroups)
                        .LocalNav())
                    .Add(S["Dispositions"], S["Dispositions"].PrefixPosition(), dispositions => dispositions
                        .AddClass("dispositions")
                        .Id("dispositions")
                        .Action("Index", "Dispositions", "CrestApps.OrchardCore.Omnichannel.Managements")
                        .Permission(OmnichannelConstants.Permissions.ManageDispositions)
                        .LocalNav())
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
