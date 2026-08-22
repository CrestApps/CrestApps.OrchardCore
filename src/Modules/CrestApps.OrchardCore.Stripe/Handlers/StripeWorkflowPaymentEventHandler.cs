using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Workflows;

namespace CrestApps.OrchardCore.Stripe.Handlers;

/// <summary>
/// Bridges provider-neutral payment events raised by Stripe to Stripe workflow events, so operators can
/// react to payments, failures, refunds, and disputes with custom workflows.
/// </summary>
public sealed class StripeWorkflowPaymentEventHandler : PaymentEventBase
{
    private readonly StripeWorkflowNotifier _notifier;

    /// <summary>
    /// Initializes a new instance of the <see cref="StripeWorkflowPaymentEventHandler"/> class.
    /// </summary>
    /// <param name="notifier">The notifier used to raise Stripe workflow events.</param>
    public StripeWorkflowPaymentEventHandler(StripeWorkflowNotifier notifier)
    {
        _notifier = notifier;
    }

    /// <inheritdoc/>
    public override Task PaymentSucceededAsync(PaymentSucceededContext context)
    {
        if (!IsStripe(context))
        {
            return Task.CompletedTask;
        }

        var input = CreateInput(context);
        input["AmountPaid"] = context.AmountPaid;
        input["Currency"] = context.Currency;
        input["TransactionId"] = context.TransactionId;
        input["Reason"] = context.Reason.ToString();
        input["SubscriptionId"] = context.Subscription?.SubscriptionId;

        return _notifier.TriggerAsync(StripeWorkflowEventNames.PaymentReceived, input, context.TransactionId);
    }

    /// <inheritdoc/>
    public override Task CustomerSubscriptionCreatedAsync(CustomerSubscriptionCreatedContext context)
    {
        if (!IsStripe(context))
        {
            return Task.CompletedTask;
        }

        var input = CreateInput(context);
        input["SubscriptionId"] = context.SubscriptionId;
        input["PlanId"] = context.PlanId;
        input["PlanAmount"] = context.PlanAmount;
        input["PlanCurrency"] = context.PlanCurrency;
        input["PlanInterval"] = context.PlanInterval;

        return _notifier.TriggerAsync(StripeWorkflowEventNames.SubscriptionCreated, input, context.SubscriptionId);
    }

    /// <inheritdoc/>
    public override Task PaymentIntentSucceededAsync(PaymentIntentSucceededContext context)
    {
        if (!IsStripe(context))
        {
            return Task.CompletedTask;
        }

        var input = CreateInput(context);
        input["Amount"] = context.Amount;
        input["Currency"] = context.Currency;
        input["TransactionId"] = context.TransactionId;

        return _notifier.TriggerAsync(StripeWorkflowEventNames.PaymentIntentSucceeded, input, context.TransactionId);
    }

    /// <inheritdoc/>
    public override Task PaymentFailedAsync(PaymentFailedContext context)
    {
        if (!IsStripe(context))
        {
            return Task.CompletedTask;
        }

        var input = CreateInput(context);
        input["Amount"] = context.Amount;
        input["Currency"] = context.Currency;
        input["TransactionId"] = context.TransactionId;
        input["FailureCode"] = context.FailureCode;
        input["FailureReason"] = context.FailureReason;

        return _notifier.TriggerAsync(StripeWorkflowEventNames.PaymentFailed, input, context.TransactionId);
    }

    /// <inheritdoc/>
    public override Task PaymentCanceledAsync(PaymentCanceledContext context)
    {
        if (!IsStripe(context))
        {
            return Task.CompletedTask;
        }

        var input = CreateInput(context);
        input["Currency"] = context.Currency;
        input["TransactionId"] = context.TransactionId;
        input["Reason"] = context.Reason;

        return _notifier.TriggerAsync(StripeWorkflowEventNames.PaymentCanceled, input, context.TransactionId);
    }

    /// <inheritdoc/>
    public override Task PaymentRefundedAsync(PaymentRefundedContext context)
    {
        if (!IsStripe(context))
        {
            return Task.CompletedTask;
        }

        var input = CreateInput(context);
        input["OriginalTransactionId"] = context.OriginalTransactionId;
        input["ProviderRefundReference"] = context.ProviderRefundReference;
        input["RefundedAmount"] = context.RefundedAmount;
        input["Currency"] = context.Currency;
        input["RefundStatus"] = context.RefundStatus;
        input["Reason"] = context.Reason;

        return _notifier.TriggerAsync(StripeWorkflowEventNames.PaymentRefunded, input, context.ProviderRefundReference ?? context.OriginalTransactionId);
    }

    /// <inheritdoc/>
    public override Task PaymentDisputeCreatedAsync(PaymentDisputeCreatedContext context)
    {
        if (!IsStripe(context))
        {
            return Task.CompletedTask;
        }

        var input = CreateInput(context);
        input["OriginalTransactionId"] = context.OriginalTransactionId;
        input["DisputeReference"] = context.DisputeReference;
        input["Amount"] = context.Amount;
        input["Currency"] = context.Currency;
        input["Reason"] = context.Reason;
        input["Status"] = context.Status;

        return _notifier.TriggerAsync(StripeWorkflowEventNames.DisputeCreated, input, context.DisputeReference ?? context.OriginalTransactionId);
    }

    private static bool IsStripe(PaymentEventContextBase context)
        => string.Equals(context.GatewayId, StripeConstants.ProcessorKey, StringComparison.OrdinalIgnoreCase);

    private static Dictionary<string, object> CreateInput(PaymentEventContextBase context)
    {
        var input = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            { "GatewayId", context.GatewayId },
            { "GatewayMode", context.GatewayMode.ToString() },
        };

        foreach (var item in context.Data)
        {
            input[$"Data_{item.Key}"] = item.Value;
        }

        return input;
    }
}
