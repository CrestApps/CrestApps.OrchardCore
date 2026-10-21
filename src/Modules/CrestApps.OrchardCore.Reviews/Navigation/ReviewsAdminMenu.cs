using CrestApps.OrchardCore.Reviews.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Mvc.Core.Utilities;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Reviews.Navigation;

public sealed class ReviewsAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _routeValues = new()
    {
        { "area", ReviewsConstants.Feature },
        { "controller", typeof(ReviewsAdminController).ControllerName() },
        { "action", nameof(ReviewsAdminController.Index) },
    };

    private static readonly RouteValueDictionary _settingsRouteValues = new()
    {
        { "area", "OrchardCore.Settings" },
        { "groupId", ReviewsConstants.SettingsGroupId },
    };

    internal readonly IStringLocalizer S;

    public ReviewsAdminMenu(IStringLocalizer<ReviewsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Commerce"], commerce => commerce
                .Add(S["Reviews"], S["Reviews"].PrefixPosition("9"), reviews => reviews
                    .Id("reviews")
                    .Action(_routeValues)
                    .Permission(ReviewsPermissions.ManageReviews)
                    .LocalNav()
                )
            )
            .Add(S["Settings"], settings => settings
                .Add(S["Commerce"], S["Commerce"].PrefixPosition(), commerce => commerce
                    .Add(S["Reviews"], S["Reviews"].PrefixPosition(), reviews => reviews
                        .Id("reviewsSettings")
                        .Action("Index", "Admin", _settingsRouteValues)
                        .Permission(ReviewsPermissions.ManageReviews)
                        .LocalNav()
                    )
                )
            );

        return ValueTask.CompletedTask;
    }
}
