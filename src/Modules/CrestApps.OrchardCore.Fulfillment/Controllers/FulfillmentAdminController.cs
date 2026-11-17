using CrestApps.OrchardCore.Fulfillment.Core.Models;
using CrestApps.OrchardCore.Fulfillment.Core.Services;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.Fulfillment.Controllers;

/// <summary>
/// The "Send to fulfillment partners" button on an order's admin page.
/// </summary>
public sealed class FulfillmentAdminController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IOrderStore _orderStore;
    private readonly FulfillmentSender _sender;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="FulfillmentAdminController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="orderStore">The order store.</param>
    /// <param name="sender">The fulfillment sender.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    public FulfillmentAdminController(
        IAuthorizationService authorizationService,
        IOrderStore orderStore,
        FulfillmentSender sender,
        INotifier notifier,
        IHtmlLocalizer<FulfillmentAdminController> htmlLocalizer)
    {
        _authorizationService = authorizationService;
        _orderStore = orderStore;
        _sender = sender;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Sends an order to every connected partner it was not sent to yet, and again to those that need attention.
    /// </summary>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="returnUrl">The order page to go back to.</param>
    /// <returns>A redirect to the order page.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Admin("fulfillment-partners/orders/send", "FulfillmentSendOrder")]
    public async Task<IActionResult> Send(string orderId, string returnUrl)
    {
        if (!await _authorizationService.AuthorizeAsync(User, FulfillmentPermissions.ManageFulfillmentPartners))
        {
            return Forbid();
        }

        if (string.IsNullOrEmpty(orderId) || await _orderStore.FindByIdAsync(orderId, HttpContext.RequestAborted) is not { } order)
        {
            return NotFound();
        }

        var requests = await _sender.RecordAsync(order, HttpContext.RequestAborted);

        if (requests.Count == 0)
        {
            await _notifier.WarningAsync(H["No fulfillment partner is connected."]);
        }

        foreach (var request in requests.Where(request => request.Status is not (FulfillmentRequestStatus.Sent or FulfillmentRequestStatus.Sending)))
        {
            var result = await _sender.SendAsync(request, HttpContext.RequestAborted);

            switch (result.Status)
            {
                case FulfillmentRequestStatus.Sent:
                    await _notifier.SuccessAsync(H["{0}: {1}", result.ProviderName, result.Message]);
                    break;

                case FulfillmentRequestStatus.NeedsAttention:
                    await _notifier.ErrorAsync(H["{0}: {1}", result.ProviderName, result.Message]);
                    break;

                default:
                    await _notifier.InformationAsync(H["{0}: {1}", result.ProviderName, result.Message]);
                    break;
            }
        }

        return Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToAction("Index", "Admin", new { area = "OrchardCore.Settings", groupId = FulfillmentConstants.SettingsGroupId });
    }
}
