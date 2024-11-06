using CrestApps.OrchardCore.Subscriptions.Core.Exceptions;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Entities;
using OrchardCore.Settings;
using YesSql.Services;

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
            if (step.BillingItems == null)
            {
                // Steps with no payment information can be ignored.
                continue;
            }

            foreach (var plan in step.BillingItems)
            {
                var lineItem = new InvoiceLineItem()
                {
                    Id = plan.Id,
                    Description = plan.Description,
                    Quantity = 1,
                    UnitPrice = plan.BillingAmount,
                    Subscription = plan.Subscription,
                };

                if (plan.Subscription == null)
                {
                    invoice.InitialPaymentAmount ??= 0;
                    invoice.InitialPaymentAmount += lineItem.GetLineTotal();
                    invoice.DueNow += lineItem.GetLineTotal();

                }
                else if (lineItem.Subscription.SubscriptionDayDelay == null || lineItem.Subscription.SubscriptionDayDelay == 0)
                {
                    invoice.FirstSubscriptionPaymentAmount ??= 0;
                    invoice.FirstSubscriptionPaymentAmount += lineItem.GetLineTotal();
                    invoice.DueNow += lineItem.GetLineTotal();
                }

                lineItems.Add(lineItem);
            }
        }

        var settings = await _siteService.GetSettingsAsync<SubscriptionSettings>();
        invoice.Currency = settings.Currency;
        invoice.LineItems = lineItems.ToArray();
        invoice.Subtotals = lineItems.Where(x => x.Subscription != null)
            .GroupBy(x => new BillingDurationKey(x.Subscription.DurationType, x.Subscription.BillingDuration))
            .ToDictionary(x => x.Key, x => x.Sum(y => y.UnitPrice * y.Quantity));

        if (invoice.InitialPaymentAmount.HasValue)
        {
            invoice.InitialPaymentAmount = Math.Round(invoice.InitialPaymentAmount.Value, 2);
        }

        if (invoice.FirstSubscriptionPaymentAmount.HasValue)
        {
            invoice.FirstSubscriptionPaymentAmount = Math.Round(invoice.FirstSubscriptionPaymentAmount.Value, 2);
        }

        invoice.DueNow = Math.Round(invoice.DueNow, 2);

        // TODO, add tax.
        invoice.GrandTotal = Math.Round(invoice.DueNow, 2);

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

        var paymentsMetadata = context.Flow.Session.As<PaymentsMetadata>();

        do
        {
            try
            {
                var minAllowedValue = GetMinimumAllowed(invoice.Currency);

                if (invoice.InitialPaymentAmount.HasValue && invoice.InitialPaymentAmount > minAllowedValue)
                {
                    var initialPaymentInfo = await _subscriptionPaymentSession.GetInitialPaymentInfoAsync(context.Flow.Session.SessionId);

                    if (initialPaymentInfo == null)
                    {
                        throw new DataNotFoundException("Initial Payment was not collected by the payment provider.");
                    }

                    if (invoice.InitialPaymentAmount.Value != initialPaymentInfo.Amount)
                    {
                        throw new PaymentValidationException("The received initial payment amount did not match the expected initial payment amount.");
                    }

                    paymentsMetadata.Payments.TryAdd(initialPaymentInfo.TransactionId, new PaymentInfo
                    {
                        TransactionId = initialPaymentInfo.TransactionId,
                        Status = PaymentStatus.Succeeded,
                        Amount = initialPaymentInfo.Amount ?? 0,
                        Currency = initialPaymentInfo.Currency,
                        GatewayId = initialPaymentInfo.GatewayId,
                        GatewayMode = initialPaymentInfo.GatewayMode,
                    });
                }

                if (invoice.FirstSubscriptionPaymentAmount.HasValue && invoice.FirstSubscriptionPaymentAmount > minAllowedValue)
                {
                    var subscriptionPaymentInfo = await _subscriptionPaymentSession.GetSubscriptionPaymentInfoAsync(context.Flow.Session.SessionId);

                    if (subscriptionPaymentInfo == null)
                    {
                        throw new DataNotFoundException("Subscription was not created by the payment provider.");
                    }

                    var totalSubscriptionPayments = subscriptionPaymentInfo.Payments.Where(x => x.Value.Status == PaymentStatus.Succeeded).Sum(x => x.Value.Amount);

                    if (invoice.FirstSubscriptionPaymentAmount > 0 && invoice.FirstSubscriptionPaymentAmount != totalSubscriptionPayments)
                    {
                        throw new PaymentValidationException($"The subscriptions payments received '{totalSubscriptionPayments}' did not match the expected amount of '{invoice.FirstSubscriptionPaymentAmount}'.");
                    }

                    foreach (var payment in subscriptionPaymentInfo.Payments.Values)
                    {
                        paymentsMetadata.Payments.TryAdd(payment.TransactionId, new PaymentInfo
                        {
                            TransactionId = payment.TransactionId,
                            SubscriptionId = payment.SubscriptionId,
                            Amount = payment.Amount,
                            Currency = payment.Currency,
                            GatewayId = payment.GatewayId,
                            GatewayMode = payment.GatewayMode,
                            Status = PaymentStatus.Succeeded,
                        });
                    }
                }

                // Store the payment info.
                context.Flow.Session.Put(paymentsMetadata);

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

    private static double GetMinimumAllowed(string currency)
    {
        if (StripeLimits.TryGetStripePaymentLimit(currency, out var limits))
        {
            return limits?.Minimum ?? 0;
        }

        return 0;
    }

    public override async Task CompletedAsync(SubscriptionFlowCompletedContext context)
    {
        // Now that the transaction is completed, remove the cache.
        await _subscriptionPaymentSession.RemoveAsync(context.Flow.Session.SessionId);
    }
}
