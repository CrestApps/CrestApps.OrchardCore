using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Subscriptions.Drivers.Steps;

public sealed class PaymentStepSubscriptionFlowDisplayDriver : SubscriptionFlowDisplayDriver
{
    private readonly PaymentMethodOptions _paymentMethodOptions;

    public PaymentStepSubscriptionFlowDisplayDriver(IOptions<PaymentMethodOptions> paymentMethodOptions)
    {
        _paymentMethodOptions = paymentMethodOptions.Value;
    }

    protected override string StepKey
        => SubscriptionConstants.StepKey.Payment;

    protected override IDisplayResult EditStep(SubscriptionFlow flow, BuildEditorContext context)
    {
        return Combine(
            View("PaymentStepInvoice_Edit", flow.Session.As<Invoice>())
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
}
