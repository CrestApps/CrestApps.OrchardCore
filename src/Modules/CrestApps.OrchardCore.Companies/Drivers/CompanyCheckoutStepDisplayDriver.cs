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

        return Initialize<CompanyCheckoutStepViewModel>("CompanyCheckoutStep", model =>
        {
            model.CompanyName = company.Name;
            model.IsRequired = company.Terms?.RequirePurchaseOrder == true;
            model.PurchaseOrderNumber ??= CompanyCheckoutStepService.GetPurchaseOrderNumber(flow.Session);
        }).Location("Content:5");
    }

    protected override async Task<IDisplayResult> UpdateStepAsync(CheckoutFlow flow, UpdateEditorContext context)
    {
        if (await _step.GetCompanyAsync(flow.Session) is null)
        {
            return null;
        }

        var model = new CompanyCheckoutStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix, value => value.PurchaseOrderNumber);

        foreach (var (_, problem) in await _step.SaveAsync(flow.Session, model.PurchaseOrderNumber))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.PurchaseOrderNumber), problem);
        }

        return await EditStepAsync(flow, context);
    }
}

public class CompanyCheckoutStepViewModel
{
    public string PurchaseOrderNumber { get; set; }

    public string CompanyName { get; set; }

    public bool IsRequired { get; set; }
}
