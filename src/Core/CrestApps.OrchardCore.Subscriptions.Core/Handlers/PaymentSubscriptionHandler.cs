using CrestApps.OrchardCore.Subscriptions.Core.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Subscriptions.Core.Handlers;

public sealed class PaymentSubscriptionHandler : SubscriptionHandlerBase
{
    public const string StepKey = "Payment";

    private readonly SubscriptionPaymentSession _subscriptionPaymentSession;

    internal readonly IStringLocalizer S;

    public PaymentSubscriptionHandler(
        SubscriptionPaymentSession subscriptionPaymentSession,
        IStringLocalizer<PaymentSubscriptionHandler> stringLocalizer)
    {
        _subscriptionPaymentSession = subscriptionPaymentSession;
        S = stringLocalizer;
    }

    public override Task InitializingAsync(SubscriptionFlowInitializingContext context)
    {
        context.Session.Steps.Add(new SubscriptionFlowStep()
        {
            Title = S["Payment"],
            Key = StepKey,
            Order = int.MaxValue,
        });

        return Task.CompletedTask;
    }

    public override Task InitializedAsync(SubscriptionFlowInitializedContext context)
    {
        var invoice = new Invoice();

        var lineItems = new List<InvoiceLineItem>();

        foreach (var step in context.Flow.GetSortedSteps())
        {
            if (step.Payment == null)
            {
                // Steps with no payment information can be ignored.
                continue;
            }

            var lineItem = new InvoiceLineItem()
            {
                Description = step.Title,
                Quantity = 1,
                UnitPrice = step.Payment.BillingAmount,
                DueNow = step.Payment.InitialAmount,
                BillingDuration = step.Payment.BillingDuration,
                BillingCycleLimit = step.Payment.BillingCycleLimit,
                SubscriptionDayDelay = step.Payment.SubscriptionDayDelay,
            };

            invoice.DueNow += step.Payment.InitialAmount ?? 0;

            lineItems.Add(lineItem);
        }

        invoice.LineItems = lineItems.ToArray();
        invoice.Subtotals = lineItems.GroupBy(x => new BillingDurationKey(x.DurationType, x.BillingDuration))
            .ToDictionary(x => x.Key, x => x.Sum(y => y.UnitPrice * y.Quantity));

        // TODO, add tax.
        invoice.GrandTotal = invoice.DueNow;

        context.Flow.Session.Put(invoice);

        return Task.CompletedTask;
    }

    public override Task LoadingAsync(SubscriptionFlowLoadedContext context)
    {
        if (context.Flow.GetCurrentStep()?.Key != StepKey)
        {
            return Task.CompletedTask;
        }

        // Before loading payment step, make sure all previous steps are completed.
        // Otherwise, we could process a payment before we can complete the subscription.

        foreach (var step in context.Flow.GetSortedSteps())
        {
            if (step.Key == StepKey)
            {
                // If we got this far, every step before this one was completed.
                break;
            }

            if (!context.Flow.Session.SavedSteps.ContainsKey(step.Key))
            {
                // There is a step that was not completed and should be the current step.
                context.Flow.SetCurrentStep(step.Key);

                break;
            }
        }

        return Task.CompletedTask;
    }

    public override async Task CompletingAsync(SubscriptionFlowCompletedContext context)
    {
        if (!context.Flow.Session.TryGet<Invoice>(out var invoice))
        {
            throw new InvalidOperationException("Unable to find an invoice.");
        }

        var initialPaymentInfo = await _subscriptionPaymentSession.GetInitialPaymentInfoAsync(context.Flow.Session.SessionId);

        if (initialPaymentInfo == null)
        {
            throw new InvalidOperationException("Initial Payment was not collected by the payment provider.");
        }

        if (invoice.DueNow > 0 && invoice.DueNow != initialPaymentInfo.InitialPaymentAmount)
        {
            throw new InvalidOperationException("The received initial payment amount did not match the expected initial payment amount.");
        }

        var subscriptionPaymentInfo = await _subscriptionPaymentSession.GetSubscriptionPaymentInfoAsync(context.Flow.Session.SessionId);

        if (subscriptionPaymentInfo == null)
        {
            throw new InvalidOperationException("Subscription was not created by the payment provider.");
        }

        if (context.Flow.ContentItem.ContentItemId != subscriptionPaymentInfo.PlanId)
        {
            throw new InvalidOperationException("The scheduled plan id did not match the scheduled plan id at the payment provider.");
        }
    }

    public override async Task CompletedAsync(SubscriptionFlowCompletedContext context)
    {
        // Now that the transaction is completed, remove the cache.
        await _subscriptionPaymentSession.RemoveAsync(context.Flow.Session.SessionId);
    }
}
