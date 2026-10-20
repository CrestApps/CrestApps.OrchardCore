using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Services;
using CrestApps.OrchardCore.Products.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Metadata;

namespace CrestApps.OrchardCore.Orders.Handlers;

/// <summary>
/// Issues a gift card for each gift card product an order paid for, worth what was paid for it, and emails it to the
/// buyer; and turns off the unused cards of an order that is canceled.
/// </summary>
internal sealed class GiftCardOrderEventHandler : OrderEventHandlerBase
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IGiftCardStore _store;
    private readonly IServiceProvider _serviceProvider;

    internal readonly IStringLocalizer S;

    // The order services raise the events this handles, so they're resolved when an event comes, not injected.
    public GiftCardOrderEventHandler(
        IContentDefinitionManager contentDefinitionManager,
        IGiftCardStore store,
        IServiceProvider serviceProvider,
        IStringLocalizer<GiftCardOrderEventHandler> stringLocalizer)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _store = store;
        _serviceProvider = serviceProvider;
        S = stringLocalizer;
    }

    public override async Task PaidAsync(OrderPaidContext context, CancellationToken cancellationToken = default)
    {
        var order = context.Order;
        var lines = new List<OrderLine>();

        foreach (var line in order.Lines ?? [])
        {
            if (line.Quantity > 0 && line.UnitPrice > 0m && await IsGiftCardAsync(line.ContentType))
            {
                lines.Add(line);
            }
        }

        // Paid is raised once per order, but a card is never issued twice for one.
        if (lines.Count == 0 || (await _store.ListByOrderAsync(order.ItemId, cancellationToken)).Count > 0)
        {
            return;
        }

        var giftCards = _serviceProvider.GetRequiredService<GiftCardService>();
        var emailSender = _serviceProvider.GetRequiredService<GiftCardEmailSender>();
        var orderService = _serviceProvider.GetRequiredService<IOrderService>();

        // An order paid by bank transfer is placed before the money arrives; its cards stay off until staff turn them
        // on, so a card can't be spent before it's paid for.
        var owed = await _serviceProvider.GetRequiredService<OrderOperationsService>().IsOwedAsync(order, cancellationToken);

        foreach (var line in lines)
        {
            for (var i = 0; i < line.Quantity; i++)
            {
                var card = await giftCards.IssueAsync(
                    line.UnitPrice,
                    order.Currency,
                    S["Bought with order {0}", order.OrderNumber],
                    card =>
                    {
                        card.OrderId = order.ItemId;
                        card.OrderNumber = order.OrderNumber;
                        card.RecipientName = order.CustomerName;
                        card.RecipientEmail = order.CustomerEmail;
                        card.IsEnabled = !owed;
                        card.Note = owed ? S["Turn it on when the order is paid in full, then email it."].Value : null;
                    },
                    cancellationToken: cancellationToken);

                var sent = !owed && await emailSender.SendAsync(card, message: null, cancellationToken);

                await orderService.AddNoteAsync(
                    order,
                    owed
                        ? S["Issued gift card ending {0} worth {1}, turned off until the order is paid.", card.LastCharacters, ProductPriceFormatter.Format(card.Balance, card.Currency)]
                        : sent
                            ? S["Issued gift card ending {0} worth {1} and emailed it to {2}.", card.LastCharacters, ProductPriceFormatter.Format(card.Balance, card.Currency), card.RecipientEmail]
                            : S["Issued gift card ending {0} worth {1}. It couldn't be emailed; send it from its page.", card.LastCharacters, ProductPriceFormatter.Format(card.Balance, card.Currency)],
                    OrderNoteKind.Event,
                    authorName: null,
                    cancellationToken);
            }
        }
    }

    public override Task CanceledAsync(OrderCanceledContext context, CancellationToken cancellationToken = default)
        => TurnOffUnusedAsync(context.Order, S["Turned off because order {0} was canceled.", context.Order.OrderNumber], cancellationToken);

    public override Task RefundedAsync(OrderRefundedContext context, CancellationToken cancellationToken = default)
        => context.Order.RefundStatus == OrderRefundStatus.Refunded
            ? TurnOffUnusedAsync(context.Order, S["Turned off because order {0} was refunded.", context.Order.OrderNumber], cancellationToken)
            : Task.CompletedTask;

    // A card nobody has spent from is turned off with its order; one that was spent is left for staff to decide.
    private async Task TurnOffUnusedAsync(Order order, string note, CancellationToken cancellationToken)
    {
        foreach (var card in await _store.ListByOrderAsync(order.ItemId, cancellationToken))
        {
            if (card.IsEnabled && card.Balance >= card.InitialValue)
            {
                card.IsEnabled = false;
                card.Note = note;

                await _store.UpdateAsync(card, cancellationToken);
            }
        }
    }

    private async Task<bool> IsGiftCardAsync(string contentType)
        => !string.IsNullOrEmpty(contentType) &&
            await _contentDefinitionManager.GetTypeDefinitionAsync(contentType) is { } definition &&
            definition.Parts.Any(part => string.Equals(part.PartDefinition.Name, nameof(GiftCardPart), StringComparison.Ordinal));
}
