using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Handlers;
using CrestApps.OrchardCore.Companies.Services;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Companies.Handlers;

/// <summary>
/// Adds the Company step to a company buyer's checkout, before payment, and puts its purchase order number on the order
/// as it completes.
/// </summary>
public sealed class CompanyCheckoutHandler : CheckoutHandlerBase
{
    // After contact and shipping, before payment.
    private const int StepOrder = 3;

    private readonly CompanyCheckoutStepService _step;
    private readonly IOrderStore _orders;

    internal readonly IStringLocalizer S;

    public CompanyCheckoutHandler(CompanyCheckoutStepService step, IOrderStore orders, IStringLocalizer<CompanyCheckoutHandler> stringLocalizer)
    {
        _step = step;
        _orders = orders;
        S = stringLocalizer;
    }

    public override async Task ActivatingAsync(CheckoutFlowActivatingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (await _step.GetCompanyAsync(context.Session) is not { } company ||
            context.Session.Steps.Any(step => step.Key == CompanyCheckoutStepService.StepKey))
        {
            return;
        }

        context.Session.Steps.Add(new CheckoutFlowStep
        {
            Key = CompanyCheckoutStepService.StepKey,
            Title = S["Company"],
            Description = company.Name,
            Order = StepOrder,
            CollectData = true,
        });

        // An order that already has a purchase order number, such as a quote the buyer asked for with one, doesn't ask
        // for it again: the step starts done, and the buyer can still change it.
        if (context.Session is CheckoutSession session && session.SavedSteps[CompanyCheckoutStepService.StepKey] is null &&
            string.Equals(session.ReferenceType, CheckoutReferenceTypes.Order, StringComparison.Ordinal) &&
            await _orders.FindByIdAsync(session.ReferenceId) is { PurchaseOrderNumber.Length: > 0 } order)
        {
            session.SavedSteps[CompanyCheckoutStepService.StepKey] = new JsonObject { ["PurchaseOrderNumber"] = order.PurchaseOrderNumber };
        }
    }

    public override async Task CompletingAsync(CheckoutFlowCompletingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Flow.Session is not CheckoutSession session ||
            CompanyCheckoutStepService.GetPurchaseOrderNumber(session) is not { } number ||
            !string.Equals(session.ReferenceType, CheckoutReferenceTypes.Order, StringComparison.Ordinal) ||
            await _orders.FindByIdAsync(session.ReferenceId) is not { } order ||
            string.Equals(order.PurchaseOrderNumber, number, StringComparison.Ordinal))
        {
            return;
        }

        order.PurchaseOrderNumber = number;
        await _orders.UpdateAsync(order);
    }
}
