using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core;
using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Customers.ViewModels;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using OrchardCore.ContentManagement.Display;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Customers.Drivers;

/// <summary>
/// The store's own checkout fields, under the buyer's contact details on the Contact step. What the buyer enters is
/// kept on the order as soon as the step is valid, so the store sees it on the order's page.
/// </summary>
internal sealed class CheckoutDetailsCheckoutFlowDisplayDriver : CheckoutFlowDisplayDriver
{
    private readonly CheckoutDetailsForm _form;
    private readonly IContentItemDisplayManager _contentItemDisplayManager;
    private readonly IOrderStore _orderStore;

    public CheckoutDetailsCheckoutFlowDisplayDriver(
        CheckoutDetailsForm form,
        IContentItemDisplayManager contentItemDisplayManager,
        IOrderStore orderStore)
    {
        _form = form;
        _contentItemDisplayManager = contentItemDisplayManager;
        _orderStore = orderStore;
    }

    protected override string StepKey
        => CustomersConstants.ContactStepKey;

    protected override void BuildPrefix(CheckoutFlow model, string htmlFieldPrefix)
        => Prefix = string.IsNullOrEmpty(htmlFieldPrefix) ? CustomersConstants.CheckoutDetailsContentType : htmlFieldPrefix + "." + CustomersConstants.CheckoutDetailsContentType;

    protected override async Task<IDisplayResult> EditStepAsync(CheckoutFlow flow, BuildEditorContext context)
    {
        if (!await _form.HasFieldsAsync() || await GetOrderAsync(flow) is not { } order)
        {
            return null;
        }

        var item = await _form.NewAsync(order.CheckoutDetails);
        var editor = await _contentItemDisplayManager.BuildEditorAsync(item, context.Updater, isNew: true, htmlFieldPrefix: Prefix);

        return Editor(editor);
    }

    protected override async Task<IDisplayResult> UpdateStepAsync(CheckoutFlow flow, UpdateEditorContext context)
    {
        if (!await _form.HasFieldsAsync() || await GetOrderAsync(flow) is not { } order)
        {
            return null;
        }

        var item = await _form.NewAsync(order.CheckoutDetails);
        var editor = await _contentItemDisplayManager.UpdateEditorAsync(item, context.Updater, isNew: true, htmlFieldPrefix: Prefix);

        // A field left wrong keeps the buyer on the step, with what they typed, until it is put right.
        if (context.Updater.ModelState.IsValid)
        {
            order.CheckoutDetails = CheckoutDetailsForm.GetContent(item);

            await _orderStore.UpdateAsync(order);
        }

        return Editor(editor);
    }

    private ShapeResult Editor(IShape editor)
        => Initialize<CheckoutDetailsViewModel>("CheckoutDetailsStep_Edit", model => model.Content = editor)
            .Location("Content:5");

    private async Task<Order> GetOrderAsync(CheckoutFlow flow)
        => flow.Session is CheckoutSession session &&
            string.Equals(session.ReferenceType, CheckoutReferenceTypes.Order, StringComparison.Ordinal) &&
            !string.IsNullOrEmpty(session.ReferenceId)
                ? await _orderStore.FindByIdAsync(session.ReferenceId)
                : null;
}
