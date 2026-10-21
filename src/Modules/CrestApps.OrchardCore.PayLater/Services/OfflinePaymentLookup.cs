using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;

namespace CrestApps.OrchardCore.PayLater.Services;

/// <summary>
/// Finds how a checkout was paid offline, and the balance it left.
/// </summary>
public sealed class OfflinePaymentLookup
{
    private readonly IPaymentAttemptStore _paymentAttemptStore;
    private readonly ITransactionManager _transactionManager;
    private readonly IEnumerable<OfflineCheckoutPaymentProvider> _providers;
    private readonly IEnumerable<ICheckoutReferenceDescriber> _describers;
    private readonly IServiceProvider _serviceProvider;

    public OfflinePaymentLookup(
        IPaymentAttemptStore paymentAttemptStore,
        ITransactionManager transactionManager,
        IEnumerable<OfflineCheckoutPaymentProvider> providers,
        IEnumerable<ICheckoutReferenceDescriber> describers,
        IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _paymentAttemptStore = paymentAttemptStore;
        _transactionManager = transactionManager;
        _providers = providers;
        _describers = describers;
    }

    /// <summary>
    /// Returns the reference a buyer quotes with a transfer: an order's number, else what the checkout paid for, such
    /// as <c>Invoice 1042</c>, else the checkout's own id.
    /// </summary>
    public async Task<string> GetReferenceAsync(CrestApps.OrchardCore.Checkout.CheckoutSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        // Bank reference fields are short, so an order is referred to by its number alone, as on its page.
        if (string.Equals(session.ReferenceType, CrestApps.OrchardCore.Checkout.CheckoutReferenceTypes.Order, StringComparison.Ordinal) &&
            !string.IsNullOrEmpty(session.ReferenceId) &&
            _serviceProvider.GetService<IOrderStore>() is { } orders &&
            await orders.FindByIdAsync(session.ReferenceId) is { OrderNumber.Length: > 0 } order)
        {
            return order.OrderNumber;
        }

        if (!string.IsNullOrEmpty(session.ReferenceType) && !string.IsNullOrEmpty(session.ReferenceId))
        {
            foreach (var describer in _describers)
            {
                if (await describer.DescribeAsync(session.ReferenceType, session.ReferenceId) is { Text.Length: > 0 } description)
                {
                    return description.Text;
                }
            }
        }

        return session.SessionId;
    }

    /// <summary>
    /// Returns the offline method a checkout was completed with and the balance it recorded, or
    /// <see langword="null"/> when it was paid some other way.
    /// </summary>
    public async Task<(OfflineCheckoutPaymentProvider Provider, Transaction Balance)?> FindAsync(string checkoutSessionId)
    {
        if (string.IsNullOrEmpty(checkoutSessionId))
        {
            return null;
        }

        var attempts = await _paymentAttemptStore.GetBySessionAsync(checkoutSessionId);

        foreach (var attempt in attempts)
        {
            if (attempt.State != PaymentAttemptState.Succeeded ||
                _providers.FirstOrDefault(provider => string.Equals(provider.Key, attempt.ProviderKey, StringComparison.OrdinalIgnoreCase)) is not { } provider)
            {
                continue;
            }

            return (provider, await _transactionManager.GetByObligationAsync(checkoutSessionId, attempt.ObligationId));
        }

        return null;
    }
}
