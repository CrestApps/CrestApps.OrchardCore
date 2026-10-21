using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Orders.Services;

/// <summary>
/// Emails a paid order's confirmation to the customer, the first time when the checkout completes, or again when the
/// store resends it.
/// </summary>
public sealed class OrderConfirmationSender
{
    private readonly IOrderStore _orderStore;
    private readonly OrderConfirmationEmailBuilder _emailBuilder;
    private readonly IOrderLinkService _orderLinks;
    private readonly ICommerceEmailSender _emailSender;
    private readonly IClock _clock;

    public OrderConfirmationSender(
        IOrderStore orderStore,
        OrderConfirmationEmailBuilder emailBuilder,
        IOrderLinkService orderLinks,
        ICommerceEmailSender emailSender,
        IClock clock)
    {
        _orderStore = orderStore;
        _emailBuilder = emailBuilder;
        _orderLinks = orderLinks;
        _emailSender = emailSender;
        _clock = clock;
    }

    /// <summary>
    /// Gets a value indicating whether the site can send email.
    /// </summary>
    public bool CanSend
        => _emailSender.CanSend;

    /// <summary>
    /// Sends the confirmation. A failed send is logged and returns <see langword="false"/>; it never undoes the order.
    /// </summary>
    public async Task<bool> SendAsync(Order order, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        // Written in the language the buyer shopped in.
        var sent = await _emailSender.SendAsync(new CommerceEmail
        {
            Kind = OrderEmailKinds.Confirmation,
            To = order.CustomerEmail,
            Order = order,
            Culture = order.Culture,
            Reference = order.OrderNumber,
        }, async _ =>
        {
            // A guest opens the order from the email; a signed-in customer finds it under My Orders.
            var orderUrl = order.UserId is null ? await _orderLinks.CreateUrlAsync(order) : null;
            var built = await _emailBuilder.BuildAsync(order, orderUrl);

            return new CommerceEmailContent
            {
                Subject = built.Subject,
                Text = built.Text,
                Html = built.Html,
                Values = new Dictionary<string, string>
                {
                    ["OrderUrl"] = orderUrl,
                },
            };
        }, cancellationToken);

        if (!sent)
        {
            return false;
        }

        order.ConfirmationSentUtc = _clock.UtcNow;

        await _orderStore.UpdateAsync(order, cancellationToken);

        return true;
    }
}
