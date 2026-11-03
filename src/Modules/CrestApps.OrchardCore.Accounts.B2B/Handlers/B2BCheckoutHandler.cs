using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Handlers;
using CrestApps.OrchardCore.Accounts.B2B.Services;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Accounts.B2B.Handlers;

/// <summary>
/// Adds the Business account step to an account buyer's checkout, before payment, and puts its purchase order number on the order
/// as it completes.
/// </summary>
public sealed class B2BCheckoutHandler : CheckoutHandlerBase
{
    // After contact and shipping, before payment.
    private const int StepOrder = 3;

    private readonly B2BCheckoutStepService _step;
    private readonly IOrderStore _orders;

    internal readonly IStringLocalizer S;

    public B2BCheckoutHandler(B2BCheckoutStepService step, IOrderStore orders, IStringLocalizer<B2BCheckoutHandler> stringLocalizer)
    {
        _step = step;
        _orders = orders;
        S = stringLocalizer;
    }

    public override async Task ActivatingAsync(CheckoutFlowActivatingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (await _step.GetB2BAccountAsync(context.Session) is not { } account)
        {
            // A step added before the checkout was known not to be an account's, such as a sale at a register, goes.
            foreach (var stale in context.Session.Steps.Where(step => step.Key == B2BCheckoutStepService.StepKey).ToList())
            {
                context.Session.Steps.Remove(stale);
            }

            return;
        }

        if (context.Session.Steps.Any(step => step.Key == B2BCheckoutStepService.StepKey))
        {
            return;
        }

        context.Session.Steps.Add(new CheckoutFlowStep
        {
            Key = B2BCheckoutStepService.StepKey,
            Title = S["Business account"],
            Description = account.Name,
            Order = StepOrder,
            CollectData = true,
        });

        // An order that already has a purchase order number, such as a quote the buyer asked for with one, doesn't ask
        // for it again: the step starts done, and the buyer can still change it.
        if (context.Session is CheckoutSession session && session.SavedSteps[B2BCheckoutStepService.StepKey] is null &&
            string.Equals(session.ReferenceType, CheckoutReferenceTypes.Order, StringComparison.Ordinal) &&
            await _orders.FindByIdAsync(session.ReferenceId) is { PurchaseOrderNumber.Length: > 0 } order)
        {
            session.SavedSteps[B2BCheckoutStepService.StepKey] = new JsonObject { ["PurchaseOrderNumber"] = order.PurchaseOrderNumber };
        }
    }

    public override async Task CompletingAsync(CheckoutFlowCompletingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Flow.Session is not CheckoutSession session ||
            B2BCheckoutStepService.GetPurchaseOrderNumber(session) is not { } number ||
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
