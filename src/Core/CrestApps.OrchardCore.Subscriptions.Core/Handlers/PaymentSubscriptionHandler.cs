using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Exceptions;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.Entities;
using OrchardCore.Settings;
using YesSql.Services;

namespace CrestApps.OrchardCore.Subscriptions.Core.Handlers;

/// <summary>
/// Adds payment processing to subscription flows and validates payment provider confirmations before completion.
/// </summary>
public sealed class PaymentSubscriptionHandler : SubscriptionHandlerBase
{
    /// <summary>
    /// Maximum time in seconds to attempt payment confirmation before aborting.
    /// </summary>
    private const int _maxAttempts = 60;

    private readonly SubscriptionPaymentSession _subscriptionPaymentSession;
    private readonly ISiteService _siteService;
    private readonly ISubscriptionTaxService _subscriptionTaxService;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaymentSubscriptionHandler"/> class.
    /// </summary>
    /// <param name="subscriptionPaymentSession">The cache that stores payment provider metadata during the flow.</param>
    /// <param name="siteService">The site service used to read subscription settings.</param>
    /// <param name="subscriptionTaxService">The service that applies tax to the subscription invoice.</param>
    /// <param name="logger">The logger used to record delayed payment confirmation attempts.</param>
    /// <param name="stringLocalizer">The localizer used for subscription flow step text.</param>
    public PaymentSubscriptionHandler(
        SubscriptionPaymentSession subscriptionPaymentSession,
        ISiteService siteService,
        ISubscriptionTaxService subscriptionTaxService,
        ILogger<PaymentSubscriptionHandler> logger,
        IStringLocalizer<PaymentSubscriptionHandler> stringLocalizer)
    {
        _subscriptionPaymentSession = subscriptionPaymentSession;
        _siteService = siteService;
        _subscriptionTaxService = subscriptionTaxService;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <summary>
    /// Adds the payment step and attaches the subscription plan billing items to it.
    /// </summary>
    /// <param name="context">The context for the subscription flow that is being activated.</param>
    public override Task ActivatingAsync(SubscriptionFlowActivatingContext context)
    {
        context.Session.Steps.Add(new SubscriptionFlowStep()
        {
            Title = S["Payment"],
            Key = SubscriptionConstants.StepKey.Payment,
            Order = int.MaxValue,
            CollectData = false,

            // The subscription plan billing (recurring price + optional one-time setup fee) is
            // authoritative and always attached to the payment step, so it is charged exactly once
            // regardless of how many data-collection steps (content, tenant onboarding, ...) the flow
            // contains. Other handlers may still add their own billing items to their own steps.
            BillingItems = BuildPlanBillingItems(context),
        });

        return Task.CompletedTask;
    }

    private static BillingItem[] BuildPlanBillingItems(SubscriptionFlowActivatingContext context)
    {
        if (!context.SubscriptionContentItem.TryGet<SubscriptionPart>(out var subscriptionPart) ||
            !context.SubscriptionContentItem.TryGet<ProductPart>(out var productPart))
        {
            return null;
        }

        var billingItems = new List<BillingItem>()
        {
            new()
            {
                Id = context.Session.ContentItemVersionId,
                Description = context.SubscriptionContentItem.DisplayText,
                BillingAmount = productPart.Price,
                Subscription = new SubscriptionPlan()
                {
                    SubscriptionDayDelay = subscriptionPart.SubscriptionDayDelay,
                    BillingDuration = subscriptionPart.BillingDuration,
                    DurationType = subscriptionPart.DurationType,
                    BillingCycleLimit = subscriptionPart.BillingCycleLimit,
                },
            },
        };

        if (subscriptionPart.InitialAmount.HasValue && subscriptionPart.InitialAmount.Value > 0)
        {
            billingItems.Add(new BillingItem()
            {
                Id = context.Session.ContentItemVersionId + SubscriptionConstants.InitialFeeIdPrefix,
                Description = subscriptionPart.InitialAmountDescription,
                BillingAmount = subscriptionPart.InitialAmount.Value,
            });
        }

        return billingItems.ToArray();
    }

    /// <summary>
    /// Builds the invoice, rounds payable amounts, applies tax, and stores the invoice on the session.
    /// </summary>
    /// <param name="context">The context for the subscription flow that was activated.</param>
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

        // Round monetary amounts at the currency's own precision (e.g. 0 decimals for JPY) so the
        // expected amounts match what the gateway actually charges. Rounding zero-decimal currencies to
        // two decimals would otherwise reject a valid payment during confirmation.
        var decimals = GetCurrencyDecimals(invoice.Currency);

        if (invoice.InitialPaymentAmount.HasValue)
        {
            invoice.InitialPaymentAmount = Math.Round(invoice.InitialPaymentAmount.Value, decimals, MidpointRounding.AwayFromZero);
        }

        if (invoice.FirstSubscriptionPaymentAmount.HasValue)
        {
            invoice.FirstSubscriptionPaymentAmount = Math.Round(invoice.FirstSubscriptionPaymentAmount.Value, decimals, MidpointRounding.AwayFromZero);
        }

        invoice.DueNow = Math.Round(invoice.DueNow, decimals, MidpointRounding.AwayFromZero);

        // Taxation is the authoritative source of tax. When the Taxation feature is disabled this is a
        // no-op that sets GrandTotal to DueNow; otherwise it determines tax, records the tax lines, and
        // captures an immutable snapshot on the invoice before setting the GrandTotal.
        await _subscriptionTaxService.ApplyTaxAsync(invoice, context.Flow);

        context.Flow.Session.Put(invoice);
    }

    private static int GetCurrencyDecimals(string currency)
        => string.IsNullOrEmpty(currency) ? 2 : StripeCurrency.GetDecimalPlaces(currency);

    /// <summary>
    /// Redirects the flow away from the payment step until all earlier required steps are completed.
    /// </summary>
    /// <param name="context">The context for the subscription flow that is loading.</param>
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

    /// <summary>
    /// Waits for payment provider confirmations and records verified payment metadata on the session.
    /// </summary>
    /// <param name="context">The context for the subscription flow that is completing.</param>
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

        var paymentsMetadata = context.Flow.Session.GetOrCreate<PaymentsMetadata>();

        do
        {
            try
            {
                var minAllowedValue = GetMinimumAllowed(invoice.Currency);

                if (invoice.InitialPaymentAmount.HasValue && Money.IsGreaterThan(invoice.InitialPaymentAmount.Value, minAllowedValue))
                {
                    var initialPaymentInfo = await _subscriptionPaymentSession.GetInitialPaymentInfoAsync(context.Flow.Session.SessionId);

                    if (initialPaymentInfo == null)
                    {
                        throw new DataNotFoundException("Initial Payment was not collected by the payment provider.");
                    }

                    if (!Money.AreEqual(invoice.InitialPaymentAmount, initialPaymentInfo.Amount))
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

                        // Persist the checkout tax determination with the transaction so it can be
                        // audited and reproduced without recalculating with current rules.
                        TaxAmount = invoice.TaxAmount,
                        TaxSnapshot = invoice.TaxSnapshot,
                    });
                }

                if (invoice.FirstSubscriptionPaymentAmount.HasValue && Money.IsGreaterThan(invoice.FirstSubscriptionPaymentAmount.Value, minAllowedValue))
                {
                    var subscriptionPaymentInfo = await _subscriptionPaymentSession.GetSubscriptionPaymentInfoAsync(context.Flow.Session.SessionId);

                    if (subscriptionPaymentInfo == null)
                    {
                        throw new DataNotFoundException("Subscription was not created by the payment provider.");
                    }

                    var totalSubscriptionPayments = subscriptionPaymentInfo.Payments.Where(x => x.Value.Status == PaymentStatus.Succeeded).Sum(x => x.Value.Amount);

                    if (Money.IsGreaterThan(invoice.FirstSubscriptionPaymentAmount.Value, 0) && !Money.AreEqual(invoice.FirstSubscriptionPaymentAmount.Value, totalSubscriptionPayments))
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

                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(ex, "Delaying 1 second before attempt number: {AttemptCount}", attemptCount);
                }

                await Task.Delay(1_000);
            }
            catch (Exception)
            {
                throw;
            }
        } while (true);
    }

    private static decimal GetMinimumAllowed(string currency)
    {
        if (StripeLimits.TryGetStripePaymentLimit(currency, out var limits))
        {
            return limits?.Minimum ?? 0;
        }

        return 0;
    }

    /// <summary>
    /// Removes cached payment metadata after the subscription flow completes.
    /// </summary>
    /// <param name="context">The context for the subscription flow that completed.</param>
    public override async Task CompletedAsync(SubscriptionFlowCompletedContext context)
    {
        // Now that the transaction is completed, remove the cache.
        await _subscriptionPaymentSession.RemoveAsync(context.Flow.Session.SessionId);
    }
}
