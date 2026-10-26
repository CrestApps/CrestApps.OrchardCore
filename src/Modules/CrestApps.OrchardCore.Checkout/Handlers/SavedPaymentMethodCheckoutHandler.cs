using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Checkout.Handlers;

/// <summary>
/// Adds the card a completed checkout kept (<see cref="CheckoutSession.SavePaymentMethod"/>) to the buyer's kept payment
/// methods in <see cref="ISavedPaymentMethodStore"/>, so it can be found and charged later without them present. That
/// covers every checkout that keeps the card, such as an installment plan's first payment and a booking deposit.
/// </summary>
/// <remarks>
/// Only a checkout that belongs to a signed-in user is recorded: a guest has no account to keep a card on. A provider
/// that can't read back what it kept is logged and skipped; the checkout is already paid, so this never fails it.
/// </remarks>
internal sealed class SavedPaymentMethodCheckoutHandler : CheckoutHandlerBase
{
    private readonly ISavedPaymentMethodStore _store;
    private readonly IPaymentAttemptStore _attemptStore;
    private readonly IEnumerable<ICheckoutSavedPaymentMethodProvider> _providers;
    private readonly ILogger _logger;

    public SavedPaymentMethodCheckoutHandler(
        ISavedPaymentMethodStore store,
        IPaymentAttemptStore attemptStore,
        IEnumerable<ICheckoutSavedPaymentMethodProvider> providers,
        ILogger<SavedPaymentMethodCheckoutHandler> logger)
    {
        _store = store;
        _attemptStore = attemptStore;
        _providers = providers;
        _logger = logger;
    }

    /// <inheritdoc/>
    public override async Task CompletedAsync(CheckoutFlowCompletedContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Flow.Session is not CheckoutSession { SavePaymentMethod: true } session || string.IsNullOrEmpty(session.OwnerId))
        {
            return;
        }

        foreach (var attempt in await _attemptStore.GetBySessionAsync(session.SessionId))
        {
            if (attempt.State != PaymentAttemptState.Succeeded)
            {
                continue;
            }

            var provider = _providers.FirstOrDefault(candidate => string.Equals(candidate.Key, attempt.ProviderKey, StringComparison.OrdinalIgnoreCase));

            if (provider is null)
            {
                continue;
            }

            try
            {
                var paymentMethod = await provider.GetSavedPaymentMethodAsync(attempt);

                if (paymentMethod is null || string.IsNullOrEmpty(paymentMethod.ProviderKey) || string.IsNullOrEmpty(paymentMethod.PaymentMethodReference))
                {
                    continue;
                }

                await _store.SaveAsync(session.OwnerId, paymentMethod, session.SessionId);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "The payment method kept by attempt '{AttemptId}' of checkout '{SessionId}' could not be recorded for its owner.", attempt.ItemId, session.SessionId);
            }
        }
    }
}
