using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Orders.Services;

/// <summary>
/// Records the disputes, or chargebacks, a gateway reports against an order's payment on the order, with a note, so
/// the store sees them on the order's page and in its list, and answers them in time.
/// </summary>
internal sealed class OrderDisputeRecorder : PaymentEventBase
{
    private readonly IOrderStore _orderStore;
    private readonly IOrderService _orderService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public OrderDisputeRecorder(
        IOrderStore orderStore,
        IOrderService orderService,
        IServiceProvider serviceProvider,
        IClock clock,
        IStringLocalizer<OrderDisputeRecorder> stringLocalizer)
    {
        _orderStore = orderStore;
        _orderService = orderService;
        _serviceProvider = serviceProvider;
        _clock = clock;
        S = stringLocalizer;
    }

    public override async Task PaymentDisputeCreatedAsync(PaymentDisputeCreatedContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        await RecordAsync(context.GatewayId, context.OriginalTransactionId, context.DisputeReference, context.Amount, context.Currency, context.Reason, context.Status, isClosed: false, cancellationToken);
    }

    public override async Task PaymentDisputeUpdatedAsync(PaymentDisputeUpdatedContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        await RecordAsync(context.GatewayId, context.OriginalTransactionId, context.DisputeReference, context.Amount, context.Currency, context.Reason, context.Status, context.IsClosed, cancellationToken);
    }

    private async Task RecordAsync(string gateway, string transactionId, string reference, decimal? amount, string currency, string reason, string status, bool isClosed, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(reference) || await FindOrderAsync(gateway, transactionId, cancellationToken) is not { } order)
        {
            return;
        }

        var now = _clock.UtcNow;
        order.Disputes ??= [];

        var dispute = order.Disputes.FirstOrDefault(entry => string.Equals(entry.Reference, reference, StringComparison.Ordinal));
        var isNew = dispute is null;

        // A webhook delivered twice, or the same status reported again, changes nothing and notes nothing.
        if (!isNew && string.Equals(dispute.Status, status, StringComparison.Ordinal) && dispute.IsClosed == isClosed)
        {
            return;
        }

        if (isNew)
        {
            dispute = new OrderDispute
            {
                Reference = reference,
                Gateway = gateway,
                CreatedUtc = now,
            };

            order.Disputes.Add(dispute);
        }

        dispute.Amount = amount ?? dispute.Amount;
        dispute.Currency = currency?.ToUpperInvariant() ?? dispute.Currency ?? order.Currency;
        dispute.Reason = reason ?? dispute.Reason;
        dispute.Status = status;
        dispute.IsClosed = isClosed;
        dispute.UpdatedUtc = now;

        await _orderStore.UpdateAsync(order, cancellationToken);

        var formatted = ProductPriceFormatter.Format(dispute.Amount, dispute.Currency ?? order.Currency);
        var note = isNew
            ? S["The payer disputed {0} with their bank ({1}). Respond from the payment provider's dashboard before its deadline.", formatted, OrderDisputeText.Reason(S, dispute.Reason)]
            : S["Dispute of {0}: {1}.", formatted, OrderDisputeText.Status(S, dispute.Status)];

        await _orderService.AddNoteAsync(order, note, OrderNoteKind.Event, null, cancellationToken);
    }

    // The dispute names the gateway's payment; the attempt that made it names the checkout, and the checkout its order.
    private async Task<Order> FindOrderAsync(string gateway, string transactionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(transactionId) ||
            _serviceProvider.GetService<IPaymentAttemptStore>() is not { } attempts ||
            _serviceProvider.GetService<ICheckoutSessionStore>() is not { } sessions)
        {
            return null;
        }

        var found = await attempts.PageAsync(1, 1, new PaymentAttemptQuery
        {
            ProviderKey = gateway,
            ProviderReference = transactionId,
        }, cancellationToken);

        var attempt = found.Entries.FirstOrDefault();

        if (attempt is null || string.IsNullOrEmpty(attempt.SessionId))
        {
            return null;
        }

        var matches = await sessions.GetManyAsync([attempt.SessionId], cancellationToken);
        var session = matches.Count > 0 ? matches[0] : null;

        return session is not null &&
            string.Equals(session.ReferenceType, CheckoutReferenceTypes.Order, StringComparison.Ordinal) &&
            !string.IsNullOrEmpty(session.ReferenceId)
                ? await _orderStore.FindByIdAsync(session.ReferenceId, cancellationToken)
                : null;
    }
}
