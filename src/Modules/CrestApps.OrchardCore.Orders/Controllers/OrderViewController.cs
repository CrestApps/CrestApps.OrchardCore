using CrestApps.OrchardCore.Core.Throttling;
using CrestApps.OrchardCore.Throttling;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Commerce.Emails;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Services;
using CrestApps.OrchardCore.Orders.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.Modules;
using OrchardCore.RateLimits;

namespace CrestApps.OrchardCore.Orders.Controllers;

/// <summary>
/// The pages a guest reaches without signing in: an order opened from its signed link, and Find my order, which emails
/// those links to the address an order was placed with. Each page's content is a shape (<c>GuestOrder</c>,
/// <c>GuestOrderInvalid</c>, <c>OrderLookup</c>, <c>OrderLookupSent</c>) that a theme can override.
/// </summary>
public sealed class OrderViewController : Controller
{
    private const int FindLimit = 10;

    private static readonly TimeSpan _findWindow = TimeSpan.FromDays(365);
    private static readonly System.ComponentModel.DataAnnotations.EmailAddressAttribute _emailAddress = new();

    private readonly IOrderStore _orderStore;
    private readonly IOrderLinkService _orderLinks;
    private readonly OrderLinksEmailBuilder _linksEmailBuilder;
    private readonly IAttemptLimiter _limiter;
    private readonly IClock _clock;
    private readonly IShapeFactory _shapeFactory;
    private readonly ICommerceEmailSender _emailSender;

    internal readonly IStringLocalizer S;

    public OrderViewController(
        IOrderStore orderStore,
        IOrderLinkService orderLinks,
        OrderLinksEmailBuilder linksEmailBuilder,
        IAttemptLimiter limiter,
        IClock clock,
        IShapeFactory shapeFactory,
        ICommerceEmailSender emailSender,
        IStringLocalizer<OrderViewController> stringLocalizer)
    {
        _orderStore = orderStore;
        _orderLinks = orderLinks;
        _linksEmailBuilder = linksEmailBuilder;
        _limiter = limiter;
        _clock = clock;
        _shapeFactory = shapeFactory;
        _emailSender = emailSender;
        S = stringLocalizer;
    }

    [HttpGet("orders/view/{token}", Name = OrderConstants.RouteNames.View)]
    public async Task<IActionResult> Index(
        string token,
        [FromServices] IDisplayManager<Order> displayManager,
        [FromServices] IUpdateModelAccessor updateModelAccessor,
        [FromServices] OrderPayLinks payLinks)
    {
        var orderId = _orderLinks.GetOrderId(token);
        var order = string.IsNullOrEmpty(orderId) ? null : await _orderStore.FindByIdAsync(orderId);

        // A link that was altered, expired or names an order that was never placed shows nothing about any order. An
        // order the store sent the buyer to pay shows before it's paid.
        if (order is null || order.Status == OrderStatus.Canceled || (order.Status == OrderStatus.Draft && order.Invoice?.SentUtc is null))
        {
            return View("Invalid", await _shapeFactory.CreateAsync("GuestOrderInvalid"));
        }

        var sections = await displayManager.BuildDisplayAsync(order, updateModelAccessor.ModelUpdater, OrderConstants.DisplayTypes.Detail);
        var payUrl = await payLinks.GetPayUrlAsync(order, Url.RouteUrl("GuestOrderPay", new { token }));
        var registerUrl = order.UserId is null && User.Identity?.IsAuthenticated != true
            ? Url.Action("Register", "Registration", new { area = "OrchardCore.Users" })
            : null;

        return View(await _shapeFactory.CreateAsync<OrderViewViewModel>("GuestOrder", model =>
        {
            model.Order = order;
            model.Sections = sections;
            model.PayUrl = payUrl;
            model.RegisterUrl = registerUrl;
            model.InvoiceUrl = Url.RouteUrl("GuestOrderInvoice", new { token });
        }));
    }

    /// <summary>
    /// The invoice of an order opened from its signed link, to print or save as a PDF.
    /// </summary>
    [HttpGet("orders/view/{token}/invoice", Name = "GuestOrderInvoice")]
    public async Task<IActionResult> Invoice(string token, [FromServices] OrderDocumentBuilder documents)
    {
        var orderId = _orderLinks.GetOrderId(token);
        var order = string.IsNullOrEmpty(orderId) ? null : await _orderStore.FindByIdAsync(orderId);

        if (order is null || order.Status is OrderStatus.Draft or OrderStatus.Canceled)
        {
            return View("Invalid", await _shapeFactory.CreateAsync("GuestOrderInvalid"));
        }

        var document = await documents.BuildInvoiceAsync(order, HttpContext.RequestAborted);

        return View(await _shapeFactory.CreateAsync<OrderDocumentViewModel>("OrderInvoice", model => OrdersAdminController.Copy(document, model)));
    }

    [HttpGet("orders/track", Name = "OrdersTrack")]
    public async Task<IActionResult> Track()
        => View(await _shapeFactory.CreateAsync<TrackOrderViewModel>("OrderTrack", model => { }));

    [HttpPost("orders/track")]
    [ValidateAntiForgeryToken]
    [RateLimitGroup(OrderConstants.FindRateLimitGroup)]
    public async Task<IActionResult> Track(TrackOrderViewModel model)
    {
        var number = model.OrderNumber?.Trim().TrimStart('#');
        var email = model.Email?.Trim();

        if (string.IsNullOrEmpty(number) || string.IsNullOrEmpty(email))
        {
            return View(await _shapeFactory.CreateAsync<TrackOrderViewModel>("OrderTrack", shape =>
            {
                shape.OrderNumber = model.OrderNumber;
                shape.Email = model.Email;
                shape.NotFound = true;
            }));
        }

        // Knowing an order's number and e-mail address doesn't open it: its link goes to that address, and the answer is
        // the same whether or not they matched. Guesses are limited by network and by e-mail address.
        if ((await _limiter.AcquireAsync(OrderConstants.AttemptPolicies.OrderLookupNetwork, ClientNetworkKey.NetworkOnly(HttpContext))).Allowed &&
            (await _limiter.AcquireAsync(OrderConstants.AttemptPolicies.OrderLookupEmail, email.ToUpperInvariant())).Allowed &&
            await _orderStore.FindByNumberAsync(number.ToUpperInvariant()) is { Status: OrderStatus.Paid } order &&
            string.Equals(order.CustomerEmail?.Trim(), email, StringComparison.OrdinalIgnoreCase))
        {
            await SendOrderLinkAsync(order);
        }

        return View("FindSent", await LookupShapeAsync("OrderLookupSent", email, emailUnavailable: !_emailSender.CanSend));
    }

    [HttpGet("orders/find", Name = OrderConstants.RouteNames.Find)]
    public async Task<IActionResult> Find()
        => View(await LookupShapeAsync("OrderLookup", email: null));

    [HttpPost("orders/find")]
    [ValidateAntiForgeryToken]
    [RateLimitGroup(OrderConstants.FindRateLimitGroup)]
    public async Task<IActionResult> Find(FindOrderViewModel model)
    {
        var email = model.Email?.Trim();

        if (string.IsNullOrEmpty(email) || !_emailAddress.IsValid(email))
        {
            ModelState.AddModelError(nameof(model.Email), S["Enter the email you placed the order with."]);

            return View(await LookupShapeAsync("OrderLookup", model.Email));
        }

        // The answer is the same whether or not there are orders, so the page never tells who has bought here. The
        // limits keep it from being used to send someone a flood of email.
        if ((await _limiter.AcquireAsync(OrderConstants.AttemptPolicies.OrderLookupNetwork, ClientNetworkKey.NetworkOnly(HttpContext))).Allowed &&
            (await _limiter.AcquireAsync(OrderConstants.AttemptPolicies.OrderLookupEmail, email.ToUpperInvariant())).Allowed)
        {
            await SendLinksAsync(email);
        }

        return View("FindSent", await LookupShapeAsync("OrderLookupSent", email, emailUnavailable: !_emailSender.CanSend));
    }

    // Find my order and its answer are the OrderLookup and OrderLookupSent shapes, so a theme can override them. A store
    // that can't send email says so on the answer instead of telling the buyer a link is on its way.
    private ValueTask<IShape> LookupShapeAsync(string shapeType, string email, bool emailUnavailable = false)
        => _shapeFactory.CreateAsync<FindOrderViewModel>(shapeType, model =>
        {
            model.Email = email;
            model.EmailUnavailable = emailUnavailable;
        });

    private async Task SendLinksAsync(string email)
    {
        if (!_emailSender.CanSend)
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

        await SendAsync(email, recent);
    }

    // The order's own link, to the address it was placed with.
    private async Task SendOrderLinkAsync(Order order)
    {
        if (_emailSender.CanSend && await _orderLinks.CreateUrlAsync(order) is { } url)
        {
            await SendAsync(order.CustomerEmail.Trim(), [(order, url)]);
        }
    }

    private async Task SendAsync(string email, List<(Order Order, string Url)> recent)
    {
        if (recent.Count == 0)
        {
            return;
        }

        await _emailSender.SendAsync(new CommerceEmail
        {
            To = email,
            Reference = "Find my order",
        }, async _ =>
        {
            var message = await _linksEmailBuilder.BuildAsync(recent);

            return new CommerceEmailContent
            {
                Subject = message.Subject,
                Text = message.Text,
                Html = message.Html,
            };
        });
    }

    // What is still owed on an order paid later, payable from its own link.
}
