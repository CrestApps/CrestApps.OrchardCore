using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Handlers;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Payments;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Checkout.Core.Handlers;

/// <summary>
/// The core checkout handler that owns the reusable payment step. It contributes the payment step to every
/// checkout, builds the single authoritative <see cref="CheckoutInvoice"/> from the billing items every
/// other step contributed, applies taxation through the <see cref="ICheckoutTaxService"/> seam, keeps the
/// customer from reaching payment before the earlier steps are complete, and — critically — only lets the
/// checkout complete once every payment obligation has been independently verified against the provider.
/// </summary>
public sealed class PaymentCheckoutHandler : CheckoutHandlerBase
{
    /// <summary>
    /// The maximum number of times completion re-verifies outstanding obligations before giving up, so a
    /// provider that is still finalizing a charge is given a bounded window to reach a terminal state.
    /// </summary>
    private const int MaxCompletionAttempts = 60;

    private readonly ISiteService _siteService;
    private readonly ICheckoutTaxService _taxService;
    private readonly ICheckoutReconciliationService _reconciliationService;
    private readonly PaymentSessionCache _paymentSessionCache;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public PaymentCheckoutHandler(
        ISiteService siteService,
        ICheckoutTaxService taxService,
        ICheckoutReconciliationService reconciliationService,
        PaymentSessionCache paymentSessionCache,
        ILogger<PaymentCheckoutHandler> logger,
        IStringLocalizer<PaymentCheckoutHandler> stringLocalizer)
    {
        _siteService = siteService;
        _taxService = taxService;
        _reconciliationService = reconciliationService;
        _paymentSessionCache = paymentSessionCache;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task ActivatingAsync(CheckoutFlowActivatingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // The payment step is always last and never collects its own data; the billing items that make up
        // the invoice are contributed by the other steps in the flow.
        context.Session.Steps.Add(new CheckoutFlowStep
        {
            Title = S["Payment"],
            Key = CheckoutConstants.PaymentStepKey,
            Order = int.MaxValue,
            CollectData = false,
        });

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override async Task ActivatedAsync(CheckoutFlowActivatedContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var settings = await _siteService.GetSettingsAsync<CheckoutSettings>();
        var currency = settings.Currency;

        var invoice = new CheckoutInvoice
        {
            Currency = currency,
        };

        var lineItems = new List<CheckoutLineItem>();

        foreach (var step in context.Flow.GetSortedSteps())
        {
            if (step.BillingItems == null)
            {
                continue;
            }

            foreach (var billingItem in step.BillingItems)
            {
                var lineItem = new CheckoutLineItem
                {
                    ItemId = billingItem.ItemId,
                    Description = billingItem.Description,
                    Quantity = 1,
                    UnitPrice = billingItem.Amount,
                    Plan = billingItem.Plan,
                };

                if (billingItem.Plan == null)
                {
                    invoice.InitialPaymentAmount ??= 0;
                    invoice.InitialPaymentAmount += lineItem.GetLineTotal(currency);
                    invoice.DueNow += lineItem.GetLineTotal(currency);
                }
                else if (billingItem.Plan.StartDayDelay is null or 0)
                {
                    invoice.FirstRecurringPaymentAmount ??= 0;
                    invoice.FirstRecurringPaymentAmount += lineItem.GetLineTotal(currency);
                    invoice.DueNow += lineItem.GetLineTotal(currency);
                }

                lineItems.Add(lineItem);
            }
        }

        invoice.LineItems = lineItems.ToArray();
        invoice.Subtotals = lineItems.Where(x => x.Plan != null)
            .GroupBy(x => new BillingDurationKey(x.Plan.DurationType, x.Plan.BillingDuration))
            .ToDictionary(x => x.Key, x => x.Sum(y => y.UnitPrice * y.Quantity));

        // Round every amount at the invoice currency's own precision so the expected amounts match what the
        // gateway actually settles. Rounding a zero-decimal currency (for example JPY) to two decimals would
        // otherwise cause a valid payment to be rejected during verification.
        if (invoice.InitialPaymentAmount.HasValue)
        {
            invoice.InitialPaymentAmount = Money.Round(invoice.InitialPaymentAmount.Value, currency);
        }

        if (invoice.FirstRecurringPaymentAmount.HasValue)
        {
            invoice.FirstRecurringPaymentAmount = Money.Round(invoice.FirstRecurringPaymentAmount.Value, currency);
        }

        invoice.DueNow = Money.Round(invoice.DueNow, currency);

        // Taxation is authoritative. When the Taxation feature is disabled this is a no-op that sets the
        // grand total to the amount due now; otherwise it determines the tax, records the tax lines, folds
        // exclusive tax into the up-front charge, and captures an immutable snapshot on the invoice.
        await _taxService.ApplyTaxAsync(invoice, context.Flow);

        context.Flow.Session.Put(invoice);
    }

    /// <inheritdoc/>
    public override Task LoadingAsync(CheckoutFlowLoadingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.Flow.CurrentStepEquals(CheckoutConstants.PaymentStepKey))
        {
            return Task.CompletedTask;
        }

        // Payment must never be reached before every earlier step is complete, otherwise a charge could be
        // taken for a checkout that can never be fulfilled.
        foreach (var step in context.Flow.GetSortedSteps())
        {
            if (step.Key == CheckoutConstants.PaymentStepKey)
            {
                break;
            }

            if (!context.Flow.Session.SavedSteps.ContainsKey(step.Key))
            {
                context.Flow.SetCurrentStep(step.Key);

                break;
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override async Task CompletingAsync(CheckoutFlowCompletingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.Flow.Session.TryGet<CheckoutInvoice>(out var invoice))
        {
            throw new CheckoutPaymentException("Unable to find a checkout invoice for the session.");
        }

        if (context.Flow.Session is not CheckoutSession session)
        {
            throw new CheckoutPaymentException("The checkout session cannot be reconciled.");
        }

        var expectedObligations = CheckoutObligations.GetExpectedObligationIds(invoice);

        // Reconcile against the providers' authoritative APIs. A provider may still be finalizing a charge,
        // so outstanding obligations are re-verified for a bounded window. A charge the provider reports as
        // failed aborts immediately; a cached notification alone never settles anything.
        var attemptCount = 0;

        while (true)
        {
            var result = await _reconciliationService.ReconcileAsync(session, expectedObligations);

            if (result.IsFullySettled)
            {
                return;
            }

            if (result.FailedObligationIds.Count > 0)
            {
                throw new CheckoutPaymentException(
                    $"The checkout could not be completed because {result.FailedObligationIds.Count} payment obligation(s) failed at the provider.");
            }

            if (attemptCount++ >= MaxCompletionAttempts)
            {
                throw new CheckoutPaymentException(
                    $"The checkout could not be completed because {result.OutstandingObligationIds.Count} payment obligation(s) were not confirmed by the provider in time.");
            }

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Waiting 1 second before re-verifying {Outstanding} outstanding checkout obligation(s), attempt {AttemptCount}.", result.OutstandingObligationIds.Count, attemptCount);
            }

            await Task.Delay(1_000);
        }
    }

    /// <inheritdoc/>
    public override async Task CompletedAsync(CheckoutFlowCompletedContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // The transaction is complete and the durable ledger is authoritative, so the short-lived payment
        // signals cached for this session are no longer needed.
        await _paymentSessionCache.RemoveAsync(context.Flow.Session.SessionId);
    }
}
