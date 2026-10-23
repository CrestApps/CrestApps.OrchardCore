using System.Security.Claims;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Stripe.Services;
using CrestApps.OrchardCore.Stripe.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.Stripe.Controllers;

/// <summary>
/// The cards a signed-in buyer saved at checkout, in their storefront account, where they remove the ones they no
/// longer want kept. It also lists the cards kept for payments taken without them, such as installments and renewals,
/// from any provider, where they choose the default one and remove the ones they no longer want charged.
/// </summary>
[Authorize]
public sealed class AccountPaymentMethodsController : Controller
{
    private readonly IStripeWalletService _walletService;
    private readonly IServiceProvider _serviceProvider;
    private readonly INotifier _notifier;
    private readonly IShapeFactory _shapeFactory;

    internal readonly IHtmlLocalizer H;

    public AccountPaymentMethodsController(
        IStripeWalletService walletService,
        IServiceProvider serviceProvider,
        INotifier notifier,
        IShapeFactory shapeFactory,
        IHtmlLocalizer<AccountPaymentMethodsController> htmlLocalizer)
    {
        _walletService = walletService;
        _serviceProvider = serviceProvider;
        _notifier = notifier;
        _shapeFactory = shapeFactory;
        H = htmlLocalizer;
    }

    [HttpGet("account/payment-methods", Name = "AccountPaymentMethods")]
    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var cards = await _walletService.ListCardsAsync(userId, HttpContext.RequestAborted);

        // The kept cards come from the checkout, which may be off; without it there are none.
        IReadOnlyList<CustomerPaymentMethod> kept = GetSavedPaymentMethodStore() is { } store
            ? await store.ListAsync(userId, HttpContext.RequestAborted)
            : [];

        // A card listed with the kept ones is not listed a second time with the saved ones.
        var keptReferences = kept
            .Select(method => method.PaymentMethod?.PaymentMethodReference)
            .Where(reference => !string.IsNullOrEmpty(reference))
            .ToHashSet(StringComparer.Ordinal);

        // The list is the AccountPaymentMethods shape, so a theme can override it.
        return View(await _shapeFactory.CreateAsync<AccountPaymentMethodsViewModel>("AccountPaymentMethods", model =>
        {
            model.Cards = [.. cards.Where(card => !keptReferences.Contains(card.Id))];
            model.KeptPaymentMethods = kept;
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

    [HttpPost("account/payment-methods/kept/{id}/default", Name = "AccountKeptPaymentMethodDefault")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MakeDefault(string id)
    {
        if (GetSavedPaymentMethodStore() is { } store &&
            await store.SetDefaultAsync(User.FindFirstValue(ClaimTypes.NameIdentifier), id, HttpContext.RequestAborted))
        {
            await _notifier.SuccessAsync(H["The card is now your default for automatic payments."]);
        }
        else
        {
            await _notifier.ErrorAsync(H["That card couldn't be found among your saved cards."]);
        }

        return RedirectToRoute("AccountPaymentMethods");
    }

    [HttpPost("account/payment-methods/kept/{id}/remove", Name = "AccountKeptPaymentMethodRemove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveKept(string id)
    {
        if (GetSavedPaymentMethodStore() is { } store &&
            await store.RemoveAsync(User.FindFirstValue(ClaimTypes.NameIdentifier), id, HttpContext.RequestAborted))
        {
            await _notifier.SuccessAsync(H["The card was removed. It is no longer charged for new automatic payments."]);
        }
        else
        {
            await _notifier.ErrorAsync(H["That card couldn't be found among your saved cards."]);
        }

        return RedirectToRoute("AccountPaymentMethods");
    }

    private ISavedPaymentMethodStore GetSavedPaymentMethodStore()
        => _serviceProvider.GetService<ISavedPaymentMethodStore>();
}
