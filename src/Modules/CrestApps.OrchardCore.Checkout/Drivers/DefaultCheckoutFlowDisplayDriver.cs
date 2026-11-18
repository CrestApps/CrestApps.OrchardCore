using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Checkout.ViewModels;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Checkout.Drivers;

/// <summary>
/// Renders the chrome shared by every checkout step: the progress stepper, the invoice summary, and the
/// navigation buttons. It is separate from the step drivers so a feature contributing a step never has to
/// re-render the surroundings, and so the customer sees a consistent frame no matter which step they are on.
/// </summary>
public sealed class DefaultCheckoutFlowDisplayDriver : DisplayDriver<CheckoutFlow>
{
    private readonly IPaymentAttemptStore _attempts;
    private readonly ICheckoutPaymentProviderResolver _providers;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultCheckoutFlowDisplayDriver"/> class.
    /// </summary>
    /// <param name="attempts">The payment attempts, which say how a completed checkout was paid.</param>
    /// <param name="providers">The payment methods, which say whether a method collects nothing now.</param>
    public DefaultCheckoutFlowDisplayDriver(IPaymentAttemptStore attempts, ICheckoutPaymentProviderResolver providers)
    {
        _attempts = attempts;
        _providers = providers;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(CheckoutFlow flow, BuildEditorContext context)
    {
        ArgumentNullException.ThrowIfNull(flow);

        var results = new List<IDisplayResult>
        {
            View("CheckoutFlowStepper", flow).Location("Steps:1"),
            View("CheckoutFlowButtons", flow).Location("Actions:10"),
        };

        // The invoice is what the customer is agreeing to pay, so it is shown alongside every step rather than
        // only at the end: a total that appears for the first time on the payment step reads as a surprise.
        if (flow.Session.TryGet<CheckoutInvoice>(out var invoice))
        {
            results.Add(Initialize<CheckoutInvoiceSummaryViewModel>("CheckoutInvoiceSummary", model => model.Invoice = invoice).Location("Aside:1"));
        }

        return Combine([.. results]);
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> DisplayAsync(CheckoutFlow flow, BuildDisplayContext context)
    {
        ArgumentNullException.ThrowIfNull(flow);

        if (!flow.Session.TryGet<CheckoutInvoice>(out var invoice))
        {
            return null;
        }

        // The confirmation says what became of the total: a checkout completed with Pay Later or a bank transfer took no
        // money, so it is due later; one a gateway charged is paid.
        var succeeded = (await _attempts.GetBySessionAsync(flow.Session.SessionId))
            .Where(attempt => attempt.State == PaymentAttemptState.Succeeded)
            .ToArray();
        var deferred = succeeded.Any(attempt => _providers.GetProvider(attempt.ProviderKey)?.Capabilities?.DefersPayment == true);

        return Initialize<CheckoutInvoiceSummaryViewModel>("CheckoutInvoiceSummary", model =>
        {
            model.Invoice = invoice;
            model.PaymentDeferred = deferred;
            model.Paid = !deferred && succeeded.Length > 0;
        }).Location("Content:10");
    }
}
