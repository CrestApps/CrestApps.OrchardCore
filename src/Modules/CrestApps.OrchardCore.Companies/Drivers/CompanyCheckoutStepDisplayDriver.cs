using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core;
using CrestApps.OrchardCore.Companies.Services;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Companies.Drivers;

/// <summary>
/// The Company step of a company buyer's checkout: who the order is for, and its purchase order number.
/// </summary>
internal sealed class CompanyCheckoutStepDisplayDriver : CheckoutFlowDisplayDriver
{
    private readonly CompanyCheckoutStepService _step;

    public CompanyCheckoutStepDisplayDriver(CompanyCheckoutStepService step)
    {
        _step = step;
    }

    protected override string StepKey
        => CompanyCheckoutStepService.StepKey;

    protected override async Task<IDisplayResult> EditStepAsync(CheckoutFlow flow, BuildEditorContext context)
    {
        if (await _step.GetCompanyAsync(flow.Session) is not { } company)
        {
            return null;
        }

        var (approval, reason) = await _step.GetApprovalAsync(flow.Session);

        return Initialize<CompanyCheckoutStepViewModel>("CompanyCheckoutStep", model =>
        {
            model.CompanyName = company.Name;
            model.IsRequired = company.Terms?.RequirePurchaseOrder == true;
            model.PurchaseOrderNumber ??= CompanyCheckoutStepService.GetPurchaseOrderNumber(flow.Session);
            model.ApprovalReason = reason;
            model.Approval = approval;
        }).Location("Content:5");
    }

    protected override async Task<IDisplayResult> UpdateStepAsync(CheckoutFlow flow, UpdateEditorContext context)
    {
        if (await _step.GetCompanyAsync(flow.Session) is null)
        {
            return null;
        }

        var model = new CompanyCheckoutStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix, value => value.PurchaseOrderNumber, value => value.RequestApproval);

        foreach (var (field, problem) in await _step.SaveAsync(flow.Session, model.PurchaseOrderNumber, model.RequestApproval))
        {
            context.Updater.ModelState.AddModelError(Prefix, field == "approval" ? nameof(model.RequestApproval) : nameof(model.PurchaseOrderNumber), problem);
        }

        return await EditStepAsync(flow, context);
    }
}

public class CompanyCheckoutStepViewModel
{
    public string PurchaseOrderNumber { get; set; }

    public string CompanyName { get; set; }

    public bool IsRequired { get; set; }

    /// <summary>
    /// Gets or sets whether the buyer asks a company admin to approve the order.
    /// </summary>
    public bool RequestApproval { get; set; }

    /// <summary>
    /// Gets or sets why the order needs approval it doesn't have, or none.
    /// </summary>
    public string ApprovalReason { get; set; }

    public Orders.Core.Models.OrderApproval Approval { get; set; }
}
