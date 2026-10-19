using CrestApps.OrchardCore.Checkout.Handlers;
using CrestApps.OrchardCore.Checkout.Services;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Checkout.Core.Handlers;

/// <summary>
/// Takes what balances pay off them as the checkout completes, and lets go of what it holds when it doesn't.
/// </summary>
/// <remarks>
/// Balances are taken while completing, not after, so taking them is saved with the checkout's completion: when it
/// fails, the checkout isn't complete, and the reconciliation sweep tries again.
/// </remarks>
public sealed class TenderCheckoutHandler : CheckoutHandlerBase
{
    private readonly ICheckoutTenderLedger _ledger;

    public TenderCheckoutHandler(ICheckoutTenderLedger ledger)
    {
        _ledger = ledger;
    }

    public override async Task CompletingAsync(CheckoutFlowCompletingContext context)
    {
        if (context.Flow.Session.TryGet<CheckoutInvoice>(out var invoice) && invoice.Tenders is { Count: > 0 })
        {
            await _ledger.RedeemAsync(context.Flow);
        }
    }

    public override Task CanceledAsync(CheckoutFlowCanceledContext context)
        => _ledger.ReleaseAsync(context.Flow.Session.SessionId);

    public override Task FailedAsync(CheckoutFlowFailedContext context)
        => _ledger.ReleaseAsync(context.Flow.Session.SessionId);

    public override Task ExpiredAsync(CheckoutFlowExpiredContext context)
        => _ledger.ReleaseAsync(context.Flow.Session.SessionId);
}
