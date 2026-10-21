using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Services;
using CrestApps.OrchardCore.Orders.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.Orders.Controllers;

/// <summary>
/// A signed-in buyer's side of quotes: asking for one from their cart, and turning one down.
/// </summary>
[Authorize]
public sealed class QuotesController : Controller
{
    private readonly QuoteService _quotes;
    private readonly IOrderStore _orders;
    private readonly INotifier _notifier;
    private readonly IShapeFactory _shapeFactory;

    internal readonly IHtmlLocalizer H;

    public QuotesController(QuoteService quotes, IOrderStore orders, INotifier notifier, IShapeFactory shapeFactory, IHtmlLocalizer<QuotesController> htmlLocalizer)
    {
        _quotes = quotes;
        _orders = orders;
        _notifier = notifier;
        _shapeFactory = shapeFactory;
        H = htmlLocalizer;
    }

    [HttpGet("cart/quote", Name = "QuoteRequest")]
    [ActionName("Request")]
    public async Task<IActionResult> RequestQuote()
    {
        if (!await _quotes.CanRequestAsync(User))
        {
            return NotFound();
        }

        return View("Request", await _shapeFactory.CreateAsync<QuoteRequestViewModel>("QuoteRequest", _ => { }));
    }

    [HttpPost("cart/quote")]
    [ValidateAntiForgeryToken]
    [ActionName("Request")]
    public async Task<IActionResult> RequestQuotePost(QuoteRequestViewModel model)
    {
        if (!await _quotes.CanRequestAsync(User))
        {
            return NotFound();
        }

        var (quote, problems) = await _quotes.RequestAsync(User, model?.Message, model?.PurchaseOrderNumber, HttpContext.RequestAborted);

        if (quote is null)
        {
            return View("Request", await _shapeFactory.CreateAsync<QuoteRequestViewModel>("QuoteRequest", form =>
            {
                form.Message = model?.Message;
                form.PurchaseOrderNumber = model?.PurchaseOrderNumber;
                form.Problems = problems;
            }));
        }

        await _notifier.SuccessAsync(H["Thanks: we've got your request, {0}. We'll email you the quote.", quote.OrderNumber]);

        return RedirectToRoute("AccountOrder", new { orderId = quote.ItemId });
    }

    [HttpPost("account/orders/{orderId}/decline-quote", Name = "AccountQuoteDecline")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Decline(string orderId)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (await _orders.FindByIdAsync(orderId, HttpContext.RequestAborted) is not { } order || order.UserId != userId)
        {
            return NotFound();
        }

        if (await _quotes.DeclineAsync(order, User.Identity?.Name, HttpContext.RequestAborted))
        {
            await _notifier.InformationAsync(H["You turned down quote {0}.", order.OrderNumber]);
        }

        return RedirectToRoute("AccountOrder", new { orderId });
    }
}
