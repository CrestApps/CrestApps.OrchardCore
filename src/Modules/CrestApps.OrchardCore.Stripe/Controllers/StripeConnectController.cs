using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Drivers;
using CrestApps.OrchardCore.Stripe.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.Stripe.Controllers;

/// <summary>
/// Verifies the Stripe secret key and provisions the webhook, or disconnects a configured Stripe account.
/// </summary>
[Admin]
public sealed class StripeConnectController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IStripeConnectService _connectService;
    private readonly LinkGenerator _linkGenerator;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="StripeConnectController"/> class.
    /// </summary>
    /// <param name="authorizationService">The service used to authorize the current user.</param>
    /// <param name="connectService">The Stripe connect service used to verify keys and manage the webhook.</param>
    /// <param name="linkGenerator">The generator used to build the absolute webhook URL.</param>
    /// <param name="notifier">The notifier used to surface user feedback.</param>
    /// <param name="htmlLocalizer">The localizer for user-facing messages.</param>
    public StripeConnectController(
        IAuthorizationService authorizationService,
        IStripeConnectService connectService,
        LinkGenerator linkGenerator,
        INotifier notifier,
        IHtmlLocalizer<StripeConnectController> htmlLocalizer)
    {
        _authorizationService = authorizationService;
        _connectService = connectService;
        _linkGenerator = linkGenerator;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Verifies the submitted (or stored) Stripe secret key and provisions the webhook for the given environment.
    /// </summary>
    /// <param name="live"><see langword="true"/> to connect the live environment; otherwise the test environment.</param>
    /// <param name="publishableKey">The publishable key entered by the administrator, when provided.</param>
    /// <param name="secretKey">The secret key entered by the administrator, when provided.</param>
    /// <returns>A redirect to the settings page with a success or failure notification.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Admin("stripe/connect", StripeConstants.RouteName.Connect)]
    public async Task<IActionResult> Connect(bool live, string publishableKey, string secretKey)
    {
        if (!await _authorizationService.AuthorizeAsync(User, StripePermissions.ManageStripeSettings))
        {
            return Forbid();
        }

        var result = await _connectService.ConnectAsync(live, publishableKey, secretKey, GetWebhookUrl());

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(H["{0}", result.Message]);
        }
        else
        {
            await _notifier.ErrorAsync(H["{0}", result.Message]);
        }

        return RedirectToSettings();
    }

    /// <summary>
    /// Disconnects the configured Stripe account for the given environment.
    /// </summary>
    /// <param name="live"><see langword="true"/> to disconnect the live environment; otherwise the test environment.</param>
    /// <returns>A redirect to the settings page with a success or failure notification.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Admin("stripe/disconnect", StripeConstants.RouteName.Disconnect)]
    public async Task<IActionResult> Disconnect(bool live)
    {
        if (!await _authorizationService.AuthorizeAsync(User, StripePermissions.ManageStripeSettings))
        {
            return Forbid();
        }

        var result = await _connectService.DisconnectAsync(live);

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(H["{0}", result.Message]);
        }
        else
        {
            await _notifier.ErrorAsync(H["{0}", result.Message]);
        }

        return RedirectToSettings();
    }

    private string GetWebhookUrl()
        => _linkGenerator.GetUriByName(HttpContext, StripeConstants.RouteName.CreateWebhookEndpoint, values: null);

    private RedirectResult RedirectToSettings()
        => Redirect(_linkGenerator.GetPathByAction(HttpContext, "Index", "Admin", new
        {
            area = "OrchardCore.Settings",
            groupId = StripeSettingsDisplayDriver.GroupId,
        }));
}
