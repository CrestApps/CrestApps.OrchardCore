using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Subscriptions.Drivers.Steps;

public sealed class PaymentStepSubscriptionFlowDisplayDriver : SubscriptionFlowDisplayDriver
{
    private readonly PaymentMethodOptions _paymentMethodOptions;

    internal readonly IStringLocalizer S;

    public PaymentStepSubscriptionFlowDisplayDriver(
        IOptions<PaymentMethodOptions> paymentMethodOptions,
        IStringLocalizer<PaymentStepSubscriptionFlowDisplayDriver> stringLocalizer)
    {
        _paymentMethodOptions = paymentMethodOptions.Value;
        S = stringLocalizer;
    }

    protected override string StepKey
        => SubscriptionConstants.StepKey.Payment;

    protected override IDisplayResult EditStep(SubscriptionFlow flow, BuildEditorContext context)
    {
        return Combine(
            View("PaymentStepInvoice_Edit", flow.Session.GetOrCreate<Invoice>())
            .Location("Content"),

            Initialize<PaymentMethodsViewModel>("PaymentMethods_Edit", model =>
            {
                model.Flow = flow;
                model.PaymentMethod = _paymentMethodOptions.DefaultPaymentMethod;
                model.PaymentMethods = _paymentMethodOptions.PaymentMethods
                .Select(x => new
                {
                    x.Key,
                    x.Value.Title,
                    x.Value.HasProcessor,
                    IsDefault = string.Equals(x.Key, _paymentMethodOptions.DefaultPaymentMethod, StringComparison.Ordinal),
                }).OrderBy(m => m.IsDefault ? 0 : 1)
                .ThenBy(x => x.Title)
                .Select(m => new SelectListItem(m.Title, m.Key))
                .ToArray();

            }).Location("Content:after")
        );
    }

    protected override async Task<IDisplayResult> UpdateStepAsync(SubscriptionFlow flow, UpdateEditorContext context)
    {
        // A subscription must never be allowed to complete when money is owed but there is no way to
        // collect it. If no payment feature (Stripe, Pay Later, ...) is enabled there are no registered
        // payment methods, so the client-side flow can never record a payment. Without this guard the
        // flow would submit, the completion handler would wait for a payment that never arrives, and the
        // customer would eventually see a generic failure. Fail fast with an actionable message instead.
        var invoice = flow.Session.GetOrCreate<Invoice>();

        if (RequiresUnavailablePaymentProvider(invoice, _paymentMethodOptions))
        {
            context.Updater.ModelState.AddModelError(
                nameof(PaymentMethodsViewModel.PaymentMethod),
                S["This subscription requires a payment, but no payment provider is enabled. Enable a payment feature (such as Stripe or Pay Later), or contact the site administrator."]);
        }

        return await EditStepAsync(flow, context);
    }

    /// <summary>
    /// Returns <see langword="true"/> when the invoice needs money collected now but there is no
    /// registered payment method to collect it (i.e. no payment feature is enabled). Completing a flow
    /// in this state can never succeed, so callers must block it.
    /// </summary>
    internal static bool RequiresUnavailablePaymentProvider(Invoice invoice, PaymentMethodOptions options)
        => PaymentIsRequired(invoice) && options.PaymentMethods.Count == 0;

    internal static bool PaymentIsRequired(Invoice invoice)
        => invoice.InitialPaymentAmount is > 0d || invoice.FirstSubscriptionPaymentAmount is > 0d;
}
