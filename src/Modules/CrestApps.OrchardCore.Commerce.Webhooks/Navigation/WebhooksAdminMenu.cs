using CrestApps.OrchardCore.Commerce.Webhooks.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Mvc.Core.Utilities;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Commerce.Webhooks.Navigation;

/// <summary>
/// Commerce › Webhooks.
/// </summary>
public sealed class WebhooksAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _routeValues = new()
    {
        { "area", WebhookConstants.Feature },
        { "controller", typeof(WebhooksAdminController).ControllerName() },
        { "action", nameof(WebhooksAdminController.Index) },
    };

    internal readonly IStringLocalizer S;

    public WebhooksAdminMenu(IStringLocalizer<WebhooksAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Commerce"], S["Commerce"].PrefixPosition(), commerce => commerce
                .Add(S["Webhooks"], S["Webhooks"].PrefixPosition("90"), webhooks => webhooks
                    .AddClass("commerce-webhooks")
                    .Id("commerceWebhooks")
                    .Action(_routeValues)
                    .Permission(WebhookPermissions.ManageWebhooks)
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}
