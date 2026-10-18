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
    private readonly ICouponRedemptionService _redemptions;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CouponRedemptionCheckoutHandler"/> class.
    /// </summary>
    /// <param name="providers">The registered discount providers.</param>
    /// <param name="redemptions">The coupon uses, released when a checkout ends unpaid.</param>
    /// <param name="logger">The logger.</param>
    public CouponRedemptionCheckoutHandler(
        IEnumerable<ICheckoutDiscountProvider> providers,
        ICouponRedemptionService redemptions,
        ILogger<CouponRedemptionCheckoutHandler> logger)
    {
        _providers = providers;
        _redemptions = redemptions;
        _logger = logger;
    }

    /// <inheritdoc/>
    public override Task CanceledAsync(CheckoutFlowCanceledContext context)
        => _redemptions.ReleaseAsync(context.Flow.Session.SessionId);

    /// <inheritdoc/>
    public override Task FailedAsync(CheckoutFlowFailedContext context)
        => _redemptions.ReleaseAsync(context.Flow.Session.SessionId);

    /// <inheritdoc/>
    public override Task ExpiredAsync(CheckoutFlowExpiredContext context)
        => _redemptions.ReleaseAsync(context.Flow.Session.SessionId);

    /// <inheritdoc/>
    public override async Task CompletedAsync(CheckoutFlowCompletedContext context)
    {
        if (!context.Flow.Session.TryGet<CheckoutInvoice>(out var invoice) || invoice.Discounts is not { Count: > 0 })
        {
            return;
        }

        // A checkout that settles an existing balance redeems nothing on completion: the module that settles the
        // balance redeems the codes once the balance is actually settled, so a code is never used up by a payment
        // that left the balance owing.
        if (context.Flow.Session is CheckoutSession { SettlesExistingBalance: true })
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
