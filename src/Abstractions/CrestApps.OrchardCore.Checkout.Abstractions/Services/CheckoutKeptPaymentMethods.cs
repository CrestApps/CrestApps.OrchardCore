using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// Reads the payment method a checkout kept for later charges (<see cref="Models.CheckoutSession.SavePaymentMethod"/>), such as
/// the card an installment plan's first payment or a booking deposit was paid with, from the provider that settled it.
/// It works for a guest's checkout too, whose card is not added to any owner's <see cref="ISavedPaymentMethodStore"/>.
/// </summary>
public static class CheckoutKeptPaymentMethods
{
    /// <summary>
    /// Returns the payment method kept by the first settled attempt of the checkout whose provider can report it, or
    /// <see langword="null"/> when none was kept.
    /// </summary>
    /// <param name="attemptStore">The payment attempts.</param>
    /// <param name="providers">The providers that can keep a payment method.</param>
    /// <param name="sessionId">The checkout that kept the payment method.</param>
    /// <param name="logger">Where a provider that failed to report it is logged.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public static async Task<SavedPaymentMethod> ReadAsync(
        IPaymentAttemptStore attemptStore,
        IEnumerable<ICheckoutSavedPaymentMethodProvider> providers,
        string sessionId,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(attemptStore);

        if (string.IsNullOrEmpty(sessionId) || providers is null)
        {
            return null;
        }

        foreach (var attempt in (await attemptStore.GetBySessionAsync(sessionId, cancellationToken)).Where(attempt => attempt.State == PaymentAttemptState.Succeeded))
        {
            var provider = providers.FirstOrDefault(candidate => string.Equals(candidate.Key, attempt.ProviderKey, StringComparison.OrdinalIgnoreCase));

            if (provider is null)
            {
                continue;
            }

            try
            {
                if (await provider.GetSavedPaymentMethodAsync(attempt, cancellationToken) is { } paymentMethod)
                {
                    return paymentMethod;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger?.LogError(ex, "Could not read the payment method kept by attempt '{AttemptId}' of checkout '{SessionId}'.", attempt.ItemId, sessionId);
            }
        }

        return null;
    }
}
