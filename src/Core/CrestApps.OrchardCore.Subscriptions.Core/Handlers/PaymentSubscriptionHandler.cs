using CrestApps.OrchardCore.Subscriptions.Core.Exceptions;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Subscriptions.Core.Handlers;

public sealed class PaymentSubscriptionHandler : SubscriptionHandlerBase
{
    /// <summary>
    /// Maximum time in seconds to attempt payment confirmation before aborting.
    /// </summary>
    private const int _maxAttempts = 60;

    private readonly SubscriptionPaymentSession _subscriptionPaymentSession;
    private readonly ISiteService _siteService;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public PaymentSubscriptionHandler(
        SubscriptionPaymentSession subscriptionPaymentSession,
        ISiteService siteService,
        ILogger<PaymentSubscriptionHandler> logger,
        IStringLocalizer<PaymentSubscriptionHandler> stringLocalizer)
    {
        _subscriptionPaymentSession = subscriptionPaymentSession;
        _siteService = siteService;
        _logger = logger;
        S = stringLocalizer;
    }

    public override Task ActivatingAsync(SubscriptionFlowActivatingContext context)
    {
        context.Session.Steps.Add(new SubscriptionFlowStep()
        {
            Title = S["Payment"],
            Key = SubscriptionConstants.StepKey.Payment,
            Order = int.MaxValue,
            CollectData = false,
        });

        return Task.CompletedTask;
    }

    public override async Task ActivatedAsync(SubscriptionFlowActivatedContext context)
    {
        var invoice = new Invoice();

        var lineItems = new List<InvoiceLineItem>();

        foreach (var step in context.Flow.GetSortedSteps())
        {
            if (step.Plan == null)
            {
                // Steps with no payment information can be ignored.
                continue;
            }

            var lineItem = new InvoiceLineItem()
            {
                Description = step.Plan.Description,
                Quantity = 1,
                UnitPrice = step.Plan.BillingAmount,
                DueNow = step.Plan.InitialAmount,
                BillingDuration = step.Plan.BillingDuration,
                BillingCycleLimit = step.Plan.BillingCycleLimit,
                SubscriptionDayDelay = step.Plan.SubscriptionDayDelay,
                InitialAmount = step.Plan.InitialAmount,
                Id = step.Plan.Id,
            };

            if (step.Plan.InitialAmount.HasValue)
            {
                invoice.InitialAmount ??= 0;
                invoice.InitialAmount += step.Plan.InitialAmount.Value;
            }

            invoice.DueNow += (step.Plan.InitialAmount ?? 0) + step.Plan.BillingAmount;

            lineItems.Add(lineItem);
        }
        var settings = await _siteService.GetSettingsAsync<SubscriptionSettings>();
        invoice.Currency = settings.Currency;
        invoice.LineItems = lineItems.ToArray();
        invoice.Subtotals = lineItems.GroupBy(x => new BillingDurationKey(x.DurationType, x.BillingDuration))
            .ToDictionary(x => x.Key, x => x.Sum(y => y.UnitPrice * y.Quantity));

        // TODO, add tax.
        invoice.GrandTotal = invoice.DueNow;

        context.Flow.Session.Put(invoice);
    }

    public override Task LoadingAsync(SubscriptionFlowLoadingContext context)
    {
        if (context.Flow.GetCurrentStep()?.Key != SubscriptionConstants.StepKey.Payment)
        {
            return Task.CompletedTask;
        }

        // Before loading payment step, make sure all previous steps are completed.
        // Otherwise, we could process a payment before we can complete the subscription.
        foreach (var step in context.Flow.GetSortedSteps())
        {
            if (step.Key == SubscriptionConstants.StepKey.Payment)
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

    public override async Task CompletingAsync(SubscriptionFlowCompletingContext context)
    {
        if (!context.Flow.Session.TryGet<Invoice>(out var invoice))
        {
            throw new InvalidOperationException("Unable to find an invoice.");
        }

        // There may be a delay between processing the payment within the app and receiving confirmation from the external provider.
        // We allow the payment provider up to '_maxAttempts' seconds to notify us that the payment was successfully processed.
        // If we don't receive confirmation within this time frame, the payment is considered failed.
        var attemptCount = 0;

        do
        {
            try
            {
                var initialPaymentInfo = await _subscriptionPaymentSession.GetInitialPaymentInfoAsync(context.Flow.Session.SessionId)
                    ?? throw new DataNotFoundException("Initial Payment was not collected by the payment provider.");

                if (invoice.DueNow > 0 && invoice.DueNow != initialPaymentInfo.InitialPaymentAmount)
                {
                    throw new PaymentValidationException("The received initial payment amount did not match the expected initial payment amount.");
                }

                var subscriptionPaymentInfo = await _subscriptionPaymentSession.GetSubscriptionPaymentInfoAsync(context.Flow.Session.SessionId)
                    ?? throw new DataNotFoundException("Subscription was not created by the payment provider.");

                if (context.Flow.ContentItem.ContentItemVersionId != subscriptionPaymentInfo.PlanId)
                {
                    throw new PaymentValidationException("The scheduled plan id did not match the scheduled plan id at the payment provider.");
                }

                // If we got here, we received the confirmation.
                break;
            }
            catch (DataNotFoundException ex)
            {
                if (attemptCount++ >= _maxAttempts)
                {
                    throw;
                }

                _logger.LogDebug(ex, "Delaying 1 second before attempt number: {AttemptCount}", attemptCount);

                await Task.Delay(1_000);
            }
            catch (Exception)
            {
                throw;
            }
        } while (true);
    }

    public override async Task CompletedAsync(SubscriptionFlowCompletedContext context)
    {
        // Now that the transaction is completed, remove the cache.
        await _subscriptionPaymentSession.RemoveAsync(context.Flow.Session.SessionId);
    }
}
