using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Services;
using CrestApps.OrchardCore.Orders.ViewModels;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.Email;
using OrchardCore.Modules;
using OrchardCore.RateLimits;

namespace CrestApps.OrchardCore.Orders.Controllers;

/// <summary>
/// The pages a guest reaches without signing in: an order opened from its signed link, and Find my order, which emails
/// those links to the address an order was placed with.
/// </summary>
public sealed class OrderViewController : Controller
{
    private const int FindLimit = 10;

    private static readonly TimeSpan _findWindow = TimeSpan.FromDays(365);
    private static readonly System.ComponentModel.DataAnnotations.EmailAddressAttribute _emailAddress = new();

    private readonly IOrderStore _orderStore;
    private readonly IOrderLinkService _orderLinks;
    private readonly OrderLinksEmailBuilder _linksEmailBuilder;
    private readonly IPaymentAttemptLimiter _limiter;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public OrderViewController(
        IOrderStore orderStore,
        IOrderLinkService orderLinks,
        OrderLinksEmailBuilder linksEmailBuilder,
        IPaymentAttemptLimiter limiter,
        IClock clock,
        ILogger<OrderViewController> logger,
        IStringLocalizer<OrderViewController> stringLocalizer)
    {
        _orderStore = orderStore;
        _orderLinks = orderLinks;
        _linksEmailBuilder = linksEmailBuilder;
        _limiter = limiter;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    [HttpGet("orders/view/{token}", Name = OrderConstants.RouteNames.View)]
    public async Task<IActionResult> Index(
        string token,
        [FromServices] IDisplayManager<Order> displayManager,
        [FromServices] IUpdateModelAccessor updateModelAccessor)
    {
        var orderId = _orderLinks.GetOrderId(token);
        var order = string.IsNullOrEmpty(orderId) ? null : await _orderStore.FindByIdAsync(orderId);

        // A link that was altered, expired or names an order that was never placed shows nothing about any order.
        if (order is null || order.Status is OrderStatus.Draft or OrderStatus.Canceled)
        {
            return View("Invalid");
        }

        return View(new OrderViewViewModel
        {
            Order = order,
            Sections = await displayManager.BuildDisplayAsync(order, updateModelAccessor.ModelUpdater, OrderConstants.DisplayTypes.Detail),
            PayUrl = await OrderPayLinks.GetPayUrlAsync(HttpContext.RequestServices, order),
            RegisterUrl = order.UserId is null && User.Identity?.IsAuthenticated != true
                ? Url.Action("Register", "Registration", new { area = "OrchardCore.Users" })
                : null,
        });
    }

    [HttpGet("orders/find", Name = OrderConstants.RouteNames.Find)]
    public IActionResult Find()
        => View(new FindOrderViewModel());

    [HttpPost("orders/find")]
    [ValidateAntiForgeryToken]
    [RateLimitGroup(OrderConstants.FindRateLimitGroup)]
    public async Task<IActionResult> Find(FindOrderViewModel model)
    {
        var email = model.Email?.Trim();

        if (string.IsNullOrEmpty(email) || !_emailAddress.IsValid(email))
        {
            ModelState.AddModelError(nameof(model.Email), S["Enter the email you placed the order with."]);

            return View(model);
        }

        // The answer is the same whether or not there are orders, so the page never tells who has bought here. The
        // limits keep it from being used to send someone a flood of email.
        if (await _limiter.AcquireAsync("find-order", HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown") &&
            await _limiter.AcquireAsync("find-order-email", email.ToUpperInvariant()))
        {
            await SendLinksAsync(email);
        }

        return View("FindSent", new FindOrderViewModel { Email = email });
    }

    private async Task SendLinksAsync(string email)
    {
        if (HttpContext.RequestServices.GetService<IEmailService>() is not { } emailService)
        {
            return;
        }

        var since = _clock.UtcNow - _findWindow;
        var (_, orders) = await _orderStore.PageAsync(new OrderQuery
        {
            CustomerEmail = email,
            Statuses = [OrderStatus.Paid, OrderStatus.AwaitingPayment],
        }, 0, FindLimit);

        var recent = new List<(Order Order, string Url)>();

        foreach (var order in orders.Where(order => order.CreatedUtc >= since))
        {
            if (await _orderLinks.CreateUrlAsync(order) is { } url)
            {
                recent.Add((order, url));
            }
        }

        if (recent.Count == 0)
        {
            return;
        }

        var message = _linksEmailBuilder.Build(recent);
        var result = await emailService.SendAsync(new MailMessage
        {
            To = email,
            Subject = message.Subject,
            TextBody = message.Text,
            HtmlBody = message.Html,
        });

        if (!result.Succeeded)
        {
            _logger.LogWarning("The links to {Count} orders could not be emailed for Find my order.", recent.Count);
        }
    }

    // What is still owed on an order paid later, payable from its own link.
}
