using CrestApps.OrchardCore.Marketing.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Mvc.Core.Utilities;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Marketing.Navigation;

/// <summary>
/// Commerce › Segments and Commerce › Campaigns.
/// </summary>
public sealed class MarketingAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _segments = new()
    {
        { "area", MarketingConstants.Feature },
        { "controller", typeof(SegmentsAdminController).ControllerName() },
        { "action", nameof(SegmentsAdminController.Index) },
    };

    private static readonly RouteValueDictionary _campaigns = new()
    {
        { "area", MarketingConstants.Feature },
        { "controller", typeof(CampaignsAdminController).ControllerName() },
        { "action", nameof(CampaignsAdminController.Index) },
    };

    internal readonly IStringLocalizer S;

    public MarketingAdminMenu(IStringLocalizer<MarketingAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Commerce"], commerce => commerce
                .Add(S["Segments"], S["Segments"].PrefixPosition("4"), segments => segments
                    .Id("commerceSegments")
                    .Action(_segments)
                    .Permission(MarketingPermissions.ManageMarketing)
                    .LocalNav())
                .Add(S["Campaigns"], S["Campaigns"].PrefixPosition("4.1"), campaigns => campaigns
                    .Id("commerceCampaigns")
                    .Action(_campaigns)
                    .Permission(MarketingPermissions.ManageMarketing)
                    .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
