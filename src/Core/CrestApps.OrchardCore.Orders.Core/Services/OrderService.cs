using System.Security.Cryptography;
using CrestApps.Core;
using CrestApps.OrchardCore.Carts.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Orders.Core.Services;

/// <summary>
/// Turns a cart into an order.
/// </summary>
public interface IOrderService
{
    /// <summary>
    /// Freezes a priced cart into a draft order. Orders made from the same cart earlier and not paid are canceled,
    /// so the customer pays for what the cart holds now.
    /// </summary>
    /// <param name="pricing">The cart, priced now. Every line must be available.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Order> CreateFromCartAsync(CartPricing pricing, CancellationToken cancellationToken = default);
}

public sealed class OrderService : IOrderService
{
    // Crockford's base 32, without the letters that read as digits.
    private const string NumberAlphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    private readonly IOrderStore _store;
    private readonly IClock _clock;

    public OrderService(IOrderStore store, IClock clock)
    {
        _store = store;
        _clock = clock;
    }

    public async Task<Order> CreateFromCartAsync(CartPricing pricing, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pricing);

        if (pricing.Cart is null || !pricing.CanCheckout)
        {
            throw new InvalidOperationException("Only a cart whose every line can be bought becomes an order.");
        }

        var now = _clock.UtcNow;

        foreach (var earlier in await _store.ListUnpaidByCartAsync(pricing.Cart.ItemId, cancellationToken))
        {
            earlier.Status = OrderStatus.Canceled;
            earlier.ModifiedUtc = now;

            await _store.UpdateAsync(earlier, cancellationToken);
        }

        var order = new Order
        {
            ItemId = UniqueId.GenerateId(),
            OrderNumber = await NewOrderNumberAsync(now, cancellationToken),
            UserId = pricing.Cart.UserId,
            CartId = pricing.Cart.ItemId,
            Currency = pricing.Currency,
            Status = OrderStatus.Draft,
            CreatedUtc = now,
            ModifiedUtc = now,
            Lines = pricing.Lines.Select(line => new OrderLine
            {
                LineId = line.Line.LineId,
                ContentItemId = line.Line.ContentItemId,
                ContentItemVersionId = line.Product?.ContentItemVersionId ?? line.ContentItem?.ContentItemVersionId,
                ContentType = line.Product?.ContentType ?? line.ContentItem?.ContentType,
                Title = line.Title,
                Sku = line.Product?.Sku,
                PriceId = line.Price?.PriceId,
                PriceName = line.Price?.Price?.Name,
                VariantId = line.Line.VariantId,
                VariantTitle = line.Product?.VariantTitle,
                Quantity = line.Line.Quantity,
                UnitPrice = line.UnitPrice,
                LineTotal = line.LineTotal,
                ProductType = line.Product?.ProductType ?? default,
                TaxCategoryCode = line.Product?.TaxCategoryCode,
                TaxClassificationCode = line.Product?.TaxClassificationCode,
            }).ToList(),
        };

        order.Subtotal = order.Lines.Sum(line => line.LineTotal);

        await _store.CreateAsync(order, cancellationToken);

        return order;
    }

    /// <summary>
    /// A number a customer can read out on the phone: the date, then six random characters, such as
    /// <c>261013-7F3K2Q</c>.
    /// </summary>
    internal static string FormatOrderNumber(DateTime utc, ReadOnlySpan<byte> random)
    {
        Span<char> suffix = stackalloc char[6];

        for (var i = 0; i < suffix.Length; i++)
        {
            suffix[i] = NumberAlphabet[random[i] % NumberAlphabet.Length];
        }

        return $"{utc:yyMMdd}-{suffix}";
    }

    private async Task<string> NewOrderNumberAsync(DateTime now, CancellationToken cancellationToken)
    {
        // A billion numbers a day makes a clash unlikely; checking makes it impossible in practice.
        for (var attempt = 0; ; attempt++)
        {
            var number = FormatOrderNumber(now, RandomNumberGenerator.GetBytes(6));

            if (attempt >= 4 || await _store.FindByNumberAsync(number, cancellationToken) is null)
            {
                return number;
            }
        }
    }
}
