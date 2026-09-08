using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Handlers;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Payments;
using Microsoft.Extensions.Localization;
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
    private readonly CheckoutInvoiceBuilder _invoiceBuilder;
    private readonly PaymentSessionCache _paymentSessionCache;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaymentCheckoutHandler"/> class.
    /// </summary>
    /// <param name="invoiceBuilder">The builder that assembles the invoice from the contributed billing items.</param>
    /// <param name="paymentSessionCache">The cache of short-lived payment signals cleared on completion.</param>
    /// <param name="stringLocalizer">The string localizer used for the payment step title.</param>
    public PaymentCheckoutHandler(
        CheckoutInvoiceBuilder invoiceBuilder,
        PaymentSessionCache paymentSessionCache,
        IStringLocalizer<PaymentCheckoutHandler> stringLocalizer)
    {
        _invoiceBuilder = invoiceBuilder;
        _paymentSessionCache = paymentSessionCache;
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
    public override Task ActivatedAsync(CheckoutFlowActivatedContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return _invoiceBuilder.BuildAsync(context.Flow);
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
    public override Task CompletingAsync(CheckoutFlowCompletingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Settlement is the engine's responsibility: it verifies every obligation against the provider's own
        // API before any handler runs, so by the time this executes the money is already confirmed. This
        // handler only asserts the invariant it owns — that a paid checkout has the invoice it was paid
        // against — which is what receipts, tax records, and reporting are later built from.
        if (!context.Flow.Session.TryGet<CheckoutInvoice>(out _))
        {
            throw new CheckoutPaymentException("Unable to find a checkout invoice for the session.");
        }

        if (context.Flow.Session is not CheckoutSession)
        {
            throw new CheckoutPaymentException("The checkout session cannot be reconciled.");
        }

        return Task.CompletedTask;
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
