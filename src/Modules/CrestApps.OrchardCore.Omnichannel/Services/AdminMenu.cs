using CrestApps.OrchardCore.Omnichannel.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Omnichannel.Services;

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
            .Add(S["Activities"], S["Activities"].PrefixPosition(), activities => activities
                .AddClass("activities")
                .Id("activities")
                .Add(S["My Activities"], S["My Activities"].PrefixPosition("1"), myActivities => myActivities
                    .AddClass("my-activities")
                    .Id("myActivities")
                    .Action("MyActivities", "Activities", "CrestApps.OrchardCore.Omnichannel")
                    .Permission(OmnichannelConstants.Permissions.ListActivities)
                    .LocalNav()
                )
                .Add(S["Campaigns"], S["Campaigns"].PrefixPosition(), campaigns => campaigns
                    .AddClass("Campaigns")
                    .Id("Campaigns")
                    .Action("Index", "Campaigns", "CrestApps.OrchardCore.Omnichannel")
                    .Permission(OmnichannelConstants.Permissions.ManageCampaigns)
                    .LocalNav()
                )
                .Add(S["Dispositions"], S["Dispositions"].PrefixPosition(), dispositions => dispositions
                    .AddClass("dispositions")
                    .Id("dispositions")
                    .Action("Index", "Dispositions", "CrestApps.OrchardCore.Omnichannel")
                    .Permission(OmnichannelConstants.Permissions.ManageDispositions)
                    .LocalNav()
                )
            , priority: 1);

        return ValueTask.CompletedTask;
    }
}

