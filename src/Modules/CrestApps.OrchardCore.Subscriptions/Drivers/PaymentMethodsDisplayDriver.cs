using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Subscriptions.Drivers;

public sealed class PaymentMethodsDisplayDriver : DisplayDriver<SubscriptionFlowPaymentMethod>
{
    private readonly PaymentMethodOptions _paymentMethodOptions;

    public PaymentMethodsDisplayDriver(IOptions<PaymentMethodOptions> paymentMethodOptions)
    {
        _paymentMethodOptions = paymentMethodOptions.Value;
    }

    public override IDisplayResult Edit(SubscriptionFlowPaymentMethod method, BuildEditorContext context)
    {
        return Initialize<PaymentMethodsViewModel>("PaymentMethodSelection", model =>
        {
            model.PaymentMethod = _paymentMethodOptions.DefaultPaymentMethod;
            model.PaymentMethods ??= [];

            var sortedMethods = new List<PaymentMethod>();

            var defaultMethod = _paymentMethodOptions.PaymentMethods.FirstOrDefault(m => m.Key == _paymentMethodOptions.DefaultPaymentMethod);
            if (defaultMethod != null)
            {
                sortedMethods.Add(defaultMethod);
                sortedMethods.AddRange(_paymentMethodOptions.PaymentMethods.Where(m => m != defaultMethod).OrderBy(m => m.HasProcessor ? 0 : 1).ThenBy(m => m.Title));
            }
            else
            {
                sortedMethods.AddRange(_paymentMethodOptions.PaymentMethods.OrderBy(m => m.HasProcessor ? 0 : 1).ThenBy(m => m.Title));
            }

            foreach (var method in sortedMethods)
            {
                model.PaymentMethods.Add(new SelectListItem()
                {
                    Text = method.Title,
                    Value = method.Key,
                });
            }
        }).Location("Method:5");
    }
}
