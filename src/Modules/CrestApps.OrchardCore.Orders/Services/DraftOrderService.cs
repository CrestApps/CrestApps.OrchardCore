using System.Text;
using System.Text.Encodings.Web;
using CrestApps.Core;
using CrestApps.OrchardCore.Carts.Core.Models;
using CrestApps.OrchardCore.Carts.Core.Services;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Products.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Email;
using OrchardCore.Modules;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Orders.Services;

/// <summary>
/// Orders the store puts together for a buyer, such as from a phone call: priced like a cart (the buyer's own prices
/// when they have an account), kept as a draft the store can change, and emailed to the buyer to pay. The buyer pays
/// through the usual checkout, where shipping and tax are added.
/// </summary>
public sealed class DraftOrderService
{
    private static readonly System.ComponentModel.DataAnnotations.EmailAddressAttribute _emailAddress = new();

    private readonly ICartService _cartService;
    private readonly IOrderService _orderService;
    private readonly IOrderStore _orderStore;
    private readonly IOrderLinkService _links;
    private readonly UserManager<IUser> _userManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly HtmlEncoder _encoder;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public DraftOrderService(
        ICartService cartService,
        IOrderService orderService,
        IOrderStore orderStore,
        IOrderLinkService links,
        UserManager<IUser> userManager,
        IServiceProvider serviceProvider,
        HtmlEncoder encoder,
        IClock clock,
        ILogger<DraftOrderService> logger,
        IStringLocalizer<DraftOrderService> stringLocalizer)
    {
        _cartService = cartService;
        _orderService = orderService;
        _orderStore = orderStore;
        _links = links;
        _userManager = userManager;
        _serviceProvider = serviceProvider;
        _encoder = encoder;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <summary>
    /// Prices the lines for the buyer, without saving anything, such as to show the store what the draft comes to.
    /// </summary>
    public async Task<CartPricing> PriceAsync(DraftOrderInput input, string cartId, CancellationToken cancellationToken = default)
    {
        var user = await FindUserAsync(input.CustomerEmail);
        var now = _clock.UtcNow;

        var cart = new Cart
        {
            ItemId = cartId ?? UniqueId.GenerateId(),
            UserId = user is null ? null : await _userManager.GetUserIdAsync(user),
            Lines = input.Lines
                .Where(line => line.Quantity > 0 && !string.IsNullOrEmpty(line.ContentItemId))
                .Select(line => new CartLine
                {
                    LineId = string.IsNullOrEmpty(line.LineId) ? UniqueId.GenerateId() : line.LineId,
                    ContentItemId = line.ContentItemId,
                    VariantId = string.IsNullOrEmpty(line.VariantId) ? null : line.VariantId,
                    Quantity = line.Quantity,
                    AddedUtc = now,
                })
                .ToList(),
            CreatedUtc = now,
            ModifiedUtc = now,
        };

        return await _cartService.PriceAsync(cart, cancellationToken);
    }

    /// <summary>
    /// Saves the draft: a new order, or new lines and details for one not checked out yet. Returns the order, or the
    /// problems for the store.
    /// </summary>
    public async Task<(Order Order, IReadOnlyList<string> Problems)> SaveAsync(Order existing, DraftOrderInput input, string actorName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var problems = new List<string>();
        var email = input.CustomerEmail?.Trim();

        if (string.IsNullOrEmpty(email) || !_emailAddress.IsValid(email))
        {
            problems.Add(S["Enter the buyer's email address; the invoice goes there."]);
        }

        if (existing is not null && (existing.Status != OrderStatus.Draft || existing.Invoice is null))
        {
            problems.Add(S["The buyer has started paying this order, so it can't be changed now."]);
        }

        var pricing = await PriceAsync(input, existing?.CartId, cancellationToken);

        if (pricing.Lines.Count == 0)
        {
            problems.Add(S["Add at least one product."]);
        }

        problems.AddRange(pricing.Lines.Where(line => line.Problem is not null).Select(line => $"{line.Title}: {line.Problem.Value}"));

        if (problems.Count > 0)
        {
            return (null, problems);
        }

        var order = existing;

        if (order is null)
        {
            var created = await _orderService.CreateFromCartAsync(pricing, cancellationToken);

            if (!created.Succeeded)
            {
                return (null, created.Problems);
            }

            order = created.Order;
            order.Invoice = new OrderInvoice { CreatedBy = actorName };

            if (!string.IsNullOrEmpty(input.AddsToOrderId) && await _orderStore.FindByIdAsync(input.AddsToOrderId, cancellationToken) is { } original)
            {
                order.Invoice.AddsToOrderId = original.ItemId;
                order.Invoice.AddsToOrderNumber = original.OrderNumber;
            }
        }
        else
        {
            var refused = await _orderService.ReplaceLinesAsync(order, pricing, cancellationToken);

            if (refused.Count > 0)
            {
                return (null, refused);
            }
        }

        order.CustomerEmail = email;
        order.CustomerName = string.IsNullOrWhiteSpace(input.CustomerName) ? order.CustomerName ?? email : input.CustomerName.Trim();
        order.UserId = pricing.Cart.UserId;
        order.Invoice.Note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim();
        order.Invoice.DueUtc = input.DueUtc;

        // With Customers enabled, an order for someone with an account belongs to their customer.
        if (order.UserId is not null && _serviceProvider.GetService<ICustomerService>() is { } customers &&
            await customers.GetOrCreateForUserAsync(order.UserId, cancellationToken) is { } customer)
        {
            order.CustomerId = customer.ItemId;
        }

        order.ModifiedUtc = _clock.UtcNow;
        await _orderStore.UpdateAsync(order, cancellationToken);

        return (order, []);
    }

    /// <summary>
    /// Emails the buyer the order and a link to pay it. Returns whether it went.
    /// </summary>
    public async Task<bool> SendInvoiceAsync(Order order, string actorName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        if (order.Invoice is null || order.Status is OrderStatus.Paid or OrderStatus.Canceled ||
            _serviceProvider.GetService<IEmailService>() is not { } emailService || string.IsNullOrWhiteSpace(order.CustomerEmail))
        {
            return false;
        }

        var viewUrl = await _links.CreateUrlAsync(order);

        if (string.IsNullOrEmpty(viewUrl))
        {
            return false;
        }

        var (subject, text, html) = BuildInvoiceEmail(order, viewUrl + "/pay");
        var result = await emailService.SendAsync(new MailMessage
        {
            To = order.CustomerEmail,
            Subject = subject,
            TextBody = text,
            HtmlBody = html,
        }, cancellationToken: cancellationToken);

        if (!result.Succeeded)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning("The invoice for order '{OrderId}' couldn't be emailed.", order.ItemId);
            }

            return false;
        }

        order.Invoice.SentUtc = _clock.UtcNow;
        await _orderStore.UpdateAsync(order, cancellationToken);
        await _orderService.AddNoteAsync(order, S["Invoice emailed to {0}.", order.CustomerEmail], OrderNoteKind.Event, actorName, cancellationToken);

        return true;
    }

    internal (string Subject, string Text, string Html) BuildInvoiceEmail(Order order, string payUrl)
    {
        var subject = S["Your order {0} is ready to pay", order.OrderNumber].Value;
        var lead = string.IsNullOrWhiteSpace(order.CustomerName)
            ? S["Here's the order we put together for you. Shipping and tax are added when you pay."].Value
            : S["Hi {0}, here's the order we put together for you. Shipping and tax are added when you pay.", order.CustomerName].Value;

        var text = new StringBuilder().AppendLine(lead).AppendLine();
        var html = new StringBuilder("<div style=\"font-family:Arial,Helvetica,sans-serif;font-size:14px;color:#212529;max-width:600px\">")
            .Append("<p>").Append(_encoder.Encode(lead)).Append("</p>");

        if (!string.IsNullOrWhiteSpace(order.Invoice?.Note))
        {
            text.AppendLine(order.Invoice.Note).AppendLine();
            html.Append("<p style=\"white-space:pre-line\">").Append(_encoder.Encode(order.Invoice.Note)).Append("</p>");
        }

        html.Append("<table style=\"border-collapse:collapse;width:100%\">");

        foreach (var line in order.Lines)
        {
            var title = OrderLineText.Describe(line);
            var amount = ProductPriceFormatter.Format(line.LineTotal, order.Currency);

            text.AppendLine($"{line.Quantity} × {title}  {amount}");
            html.Append("<tr><td style=\"padding:4px 0\">").Append(line.Quantity).Append(" × ").Append(_encoder.Encode(title)).Append("</td>")
                .Append("<td style=\"padding:4px 0;text-align:right\">").Append(_encoder.Encode(amount)).Append("</td></tr>");
        }

        var subtotal = S["Subtotal: {0}", ProductPriceFormatter.Format(order.Subtotal, order.Currency)].Value;

        text.AppendLine().AppendLine(subtotal);
        html.Append("</table><p><strong>").Append(_encoder.Encode(subtotal)).Append("</strong></p>");

        if (order.Invoice?.DueUtc is { } due)
        {
            var dueText = S["Please pay by {0:D}.", due].Value;

            text.AppendLine(dueText);
            html.Append("<p>").Append(_encoder.Encode(dueText)).Append("</p>");
        }

        var pay = S["Review and pay"].Value;

        text.AppendLine().AppendLine($"{pay}: {payUrl}");
        html.Append("<p><a href=\"").Append(_encoder.Encode(payUrl)).Append("\" style=\"display:inline-block;padding:10px 18px;background:#0d6efd;color:#fff;text-decoration:none;border-radius:4px\">")
            .Append(_encoder.Encode(pay)).Append("</a></p></div>");

        return (subject, text.ToString(), html.ToString());
    }

    private async Task<IUser> FindUserAsync(string email)
        => string.IsNullOrWhiteSpace(email) || !_emailAddress.IsValid(email.Trim()) ? null : await _userManager.FindByEmailAsync(email.Trim());
}

/// <summary>
/// What the store entered for a draft order.
/// </summary>
public sealed class DraftOrderInput
{
    public string CustomerEmail { get; init; }

    public string CustomerName { get; init; }

    public string Note { get; init; }

    public DateTime? DueUtc { get; init; }

    public string AddsToOrderId { get; init; }

    public IReadOnlyList<DraftOrderLineInput> Lines { get; init; } = [];
}

public sealed record DraftOrderLineInput(string LineId, string ContentItemId, string VariantId, int Quantity);
