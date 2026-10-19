using System.Security.Claims;
using CrestApps.OrchardCore.Stripe.Services;
using CrestApps.OrchardCore.Stripe.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.Stripe.Controllers;

/// <summary>
/// The cards a signed-in buyer saved at checkout, in their storefront account, where they remove the ones they no
/// longer want kept.
/// </summary>
[Authorize]
public sealed class AccountPaymentMethodsController : Controller
{
    private readonly IStripeWalletService _walletService;
    private readonly INotifier _notifier;
    private readonly IShapeFactory _shapeFactory;

    internal readonly IHtmlLocalizer H;

    public AccountPaymentMethodsController(
        IStripeWalletService walletService,
        INotifier notifier,
        IShapeFactory shapeFactory,
        IHtmlLocalizer<AccountPaymentMethodsController> htmlLocalizer)
    {
        _walletService = walletService;
        _notifier = notifier;
        _shapeFactory = shapeFactory;
        H = htmlLocalizer;
    }

    [HttpGet("account/payment-methods", Name = "AccountPaymentMethods")]
    public async Task<IActionResult> Index()
    {
        var cards = await _walletService.ListCardsAsync(User.FindFirstValue(ClaimTypes.NameIdentifier), HttpContext.RequestAborted);

        // The list is the AccountPaymentMethods shape, so a theme can override it.
        return View(await _shapeFactory.CreateAsync<AccountPaymentMethodsViewModel>("AccountPaymentMethods", model =>
        {
            model.Cards = cards;
        }));
    }

    [HttpPost("account/payment-methods/{paymentMethodId}/remove", Name = "AccountPaymentMethodRemove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(string paymentMethodId)
    {
        if (await _walletService.RemoveCardAsync(User.FindFirstValue(ClaimTypes.NameIdentifier), paymentMethodId, HttpContext.RequestAborted))
        {
            await _notifier.SuccessAsync(H["The card was removed."]);
        }
        else
        {
            await _notifier.ErrorAsync(H["That card couldn't be found among your saved cards."]);
        }

        return RedirectToRoute("AccountPaymentMethods");
    }
}
