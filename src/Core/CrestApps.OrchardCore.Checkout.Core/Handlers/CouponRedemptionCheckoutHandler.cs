using CrestApps.OrchardCore.Checkout.Handlers;
using CrestApps.OrchardCore.Checkout.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Checkout.Core.Handlers;

/// <summary>
/// Consumes the discounts a completed purchase actually used.
/// </summary>
/// <remarks>
/// Redeeming on completion rather than on application is what makes a usage limit real. A customer who
/// applies a single-use code and then walks away must not burn it, or a code that leaks could be exhausted
/// by people who never bought anything.
/// </remarks>
public sealed class CouponRedemptionCheckoutHandler : CheckoutHandlerBase
{
    private readonly IEnumerable<ICheckoutDiscountProvider> _providers;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CouponRedemptionCheckoutHandler"/> class.
    /// </summary>
    /// <param name="providers">The registered discount providers.</param>
    /// <param name="logger">The logger.</param>
    public CouponRedemptionCheckoutHandler(
        IEnumerable<ICheckoutDiscountProvider> providers,
        ILogger<CouponRedemptionCheckoutHandler> logger)
    {
        _providers = providers;
        _logger = logger;
    }

    /// <inheritdoc/>
    public override async Task CompletedAsync(CheckoutFlowCompletedContext context)
    {
        if (!context.Flow.Session.TryGet<CheckoutInvoice>(out var invoice) || invoice.Discounts is not { Count: > 0 })
        {
            return;
        }

        var redemption = new CheckoutDiscountRedemptionContext(context.Flow, [.. invoice.Discounts]);

        foreach (var provider in _providers)
        {
            try
            {
                await provider.RedeemAsync(redemption);
            }
            catch (Exception exception)
            {
                // The customer has already paid. A redemption counter that failed to move is a bookkeeping
                // problem to reconcile, not a reason to fail a completed purchase.
                _logger.LogError(exception, "The discount provider '{ProviderKey}' failed to record its redemptions.", provider.Key);
            }
        }
    }
}
