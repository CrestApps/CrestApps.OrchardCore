using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Customers.ViewModels;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Customers.Drivers;

/// <summary>
/// The Store credit box on the Payment step, for a signed-in buyer who has some: one click puts it toward the order.
/// </summary>
internal sealed class StoreCreditCheckoutFlowDisplayDriver : CheckoutFlowDisplayDriver
{
    /// <summary>
    /// The button that puts the buyer's store credit toward the order.
    /// </summary>
    internal const string ApplyAction = CheckoutConstants.FormActions.ApplyTender + ":" + StoreCreditService.TenderKey;

    private readonly StoreCreditTenderProvider _provider;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly CheckoutInvoiceBuilder _invoiceBuilder;

    public StoreCreditCheckoutFlowDisplayDriver(
        StoreCreditTenderProvider provider,
        IHttpContextAccessor httpContextAccessor,
        CheckoutInvoiceBuilder invoiceBuilder)
    {
        _provider = provider;
        _httpContextAccessor = httpContextAccessor;
        _invoiceBuilder = invoiceBuilder;
    }

    protected override string StepKey
        => CheckoutConstants.PaymentStepKey;

    protected override async Task<IDisplayResult> EditStepAsync(CheckoutFlow flow, BuildEditorContext context)
    {
        if (!AcceptsStoreCredit(flow) || !flow.Session.TryGet<CheckoutInvoice>(out var invoice))
        {
            return null;
        }

        var account = await _provider.FindForCheckoutAsync(flow, invoice.Currency);
        var tender = (invoice.Tenders ?? []).FirstOrDefault(entry => entry.ProviderKey == StoreCreditService.TenderKey);

        // A buyer with no credit, or an order with nothing left for it to pay, gets no box.
        if (account is null || (tender is null && (account.Balance <= 0m || invoice.InitialPaymentAmount is not > 0m)))
        {
            return null;
        }

        return Initialize<CheckoutStoreCreditViewModel>("CheckoutStoreCredit", model =>
        {
            model.AccountId = account.ItemId;
            model.Currency = invoice.Currency;
            model.Available = account.Balance;
            model.Used = tender?.Amount ?? 0m;
            model.IsApplied = tender is not null;
        }).Location("Aside:7");
    }

    protected override async Task<IDisplayResult> UpdateStepAsync(CheckoutFlow flow, UpdateEditorContext context)
    {
        if (!AcceptsStoreCredit(flow))
        {
            return null;
        }

        var action = _httpContextAccessor.HttpContext?.Request.HasFormContentType == true
            ? _httpContextAccessor.HttpContext.Request.Form[CheckoutConstants.FormActions.FieldName].ToString()
            : null;
        var removePrefix = CheckoutConstants.FormActions.RemoveTenderPrefix + StoreCreditService.TenderKey + ":";
        var selection = flow.Session.TryGet<CheckoutTenderSelection>(out var existing) ? existing : new CheckoutTenderSelection();
        var changed = false;

        if (string.Equals(action, ApplyAction, StringComparison.Ordinal) &&
            flow.Session.TryGet<CheckoutInvoice>(out var invoice) &&
            await _provider.FindForCheckoutAsync(flow, invoice.Currency) is { Balance: > 0m } account)
        {
            selection.Add(StoreCreditService.TenderKey, account.ItemId);
            changed = true;
        }
        else if (action?.StartsWith(removePrefix, StringComparison.Ordinal) == true)
        {
            changed = selection.Remove(StoreCreditService.TenderKey, action[removePrefix.Length..]);
        }

        if (changed)
        {
            flow.Session.Put(selection);

            // Rebuilt now, so the buyer sees what the credit pays and what's left before they pay.
            await _invoiceBuilder.BuildAsync(flow);
        }

        return await EditStepAsync(flow, context);
    }

    private static bool AcceptsStoreCredit(CheckoutFlow flow)
        => flow.Session is CheckoutSession { SettlesExistingBalance: false, SavePaymentMethod: false, OwnerId.Length: > 0 };
}
