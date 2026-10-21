using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Handlers;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Orders.Handlers;

/// <summary>
/// Keeps the fraud rating the gateway gave a paid order's payment on the order, so the store sees a risky order on
/// its page and in the orders list before shipping it.
/// </summary>
internal sealed class OrderPaymentRiskCheckoutHandler : CheckoutHandlerBase
{
    private static readonly string[] _levels = ["normal", "elevated", "highest"];

    private readonly IOrderStore _orderStore;
    private readonly IServiceProvider _serviceProvider;

    public OrderPaymentRiskCheckoutHandler(IOrderStore orderStore, IServiceProvider serviceProvider)
    {
        _orderStore = orderStore;
        _serviceProvider = serviceProvider;
    }

    public override async Task CompletedAsync(CheckoutFlowCompletedContext context)
    {
        if (context.Flow.Session is not CheckoutSession session ||
            !string.Equals(session.ReferenceType, CheckoutReferenceTypes.Order, StringComparison.Ordinal) ||
            string.IsNullOrEmpty(session.ReferenceId) ||
            _serviceProvider.GetService<IPaymentAttemptStore>() is not { } attempts)
        {
            return;
        }

        // The riskiest payment of the checkout speaks for the order.
        var rated = (await attempts.GetBySessionAsync(session.SessionId))
            .Where(attempt => attempt.State == PaymentAttemptState.Succeeded && !string.IsNullOrEmpty(attempt.RiskLevel))
            .OrderByDescending(attempt => Array.IndexOf(_levels, attempt.RiskLevel))
            .ThenByDescending(attempt => attempt.RiskScore ?? 0)
            .FirstOrDefault();

        if (rated is null || await _orderStore.FindByIdAsync(session.ReferenceId) is not { } order)
        {
            return;
        }

        order.PaymentRisk = new OrderPaymentRisk
        {
            Level = rated.RiskLevel,
            Score = rated.RiskScore,
            Gateway = rated.ProviderKey,
        };

        await _orderStore.UpdateAsync(order);
    }
}
