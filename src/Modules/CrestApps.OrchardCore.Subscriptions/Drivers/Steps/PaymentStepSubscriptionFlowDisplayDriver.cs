using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Subscriptions.Drivers.Steps;

public sealed class PaymentStepSubscriptionFlowDisplayDriver : SubscriptionFlowDisplayDriver
{
    protected override string StepKey
        => SubscriptionConstants.StepKey.Payment;

    protected override IDisplayResult EditStep(SubscriptionFlow flow, BuildEditorContext context)
    {
        return View("PaymentStep_Edit", flow.Session.As<Invoice>())
            .Location("Content");
    }
}
