using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core;
using CrestApps.OrchardCore.Accounts.B2B.Services;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Accounts.B2B.Drivers;

/// <summary>
/// The Business account step of an account buyer's checkout: who the order is for, and its purchase order number.
/// </summary>
internal sealed class B2BCheckoutStepDisplayDriver : CheckoutFlowDisplayDriver
{
    private readonly B2BCheckoutStepService _step;

    public B2BCheckoutStepDisplayDriver(B2BCheckoutStepService step)
    {
        _step = step;
    }

    protected override string StepKey
        => B2BCheckoutStepService.StepKey;

    protected override async Task<IDisplayResult> EditStepAsync(CheckoutFlow flow, BuildEditorContext context)
    {
        if (await _step.GetB2BAccountAsync(flow.Session) is not { } account)
        {
            return null;
        }

        var (approval, reason) = await _step.GetApprovalAsync(flow.Session);

        return Initialize<B2BCheckoutStepViewModel>("B2BCheckoutStep", model =>
        {
            model.AccountName = account.Name;
            model.IsRequired = account.Terms?.RequirePurchaseOrder == true;
            model.PurchaseOrderNumber ??= B2BCheckoutStepService.GetPurchaseOrderNumber(flow.Session);
            model.ApprovalReason = reason;
            model.Approval = approval;
        }).Location("Content:5");
    }

    protected override async Task<IDisplayResult> UpdateStepAsync(CheckoutFlow flow, UpdateEditorContext context)
    {
        if (await _step.GetB2BAccountAsync(flow.Session) is null)
        {
            return null;
        }

        var model = new B2BCheckoutStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix, value => value.PurchaseOrderNumber, value => value.RequestApproval);

        foreach (var (field, problem) in await _step.SaveAsync(flow.Session, model.PurchaseOrderNumber, model.RequestApproval))
        {
            context.Updater.ModelState.AddModelError(Prefix, field == "approval" ? nameof(model.RequestApproval) : nameof(model.PurchaseOrderNumber), problem);
        }

        return await EditStepAsync(flow, context);
    }
}

public class B2BCheckoutStepViewModel
{
    public string PurchaseOrderNumber { get; set; }

    public string AccountName { get; set; }

    public bool IsRequired { get; set; }

    /// <summary>
    /// Gets or sets whether the buyer asks an account admin to approve the order.
    /// </summary>
    public bool RequestApproval { get; set; }

    /// <summary>
    /// Gets or sets why the order needs approval it doesn't have, or none.
    /// </summary>
    public string ApprovalReason { get; set; }

    public Orders.Core.Models.OrderApproval Approval { get; set; }
}
