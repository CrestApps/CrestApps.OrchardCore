using CrestApps.OrchardCore.Webhooks.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Mvc.Core.Utilities;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Webhooks.Navigation;

/// <summary>
/// Configuration › Webhooks.
/// </summary>
public sealed class WebhooksAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _routeValues = new()
    {
        { "area", WebhooksConstants.Feature.Webhooks },
        { "controller", typeof(WebhooksAdminController).ControllerName() },
        { "action", nameof(WebhooksAdminController.Index) },
    };

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhooksAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WebhooksAdminMenu(IStringLocalizer<WebhooksAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Configuration"], configuration => configuration
                .Add(S["Webhooks"], S["Webhooks"].PrefixPosition(), webhooks => webhooks
                    .AddClass("webhooks")
                    .Id("webhooks")
                    .Action(_routeValues)
                    .Permission(WebhookPermissions.ManageWebhooks)
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}
