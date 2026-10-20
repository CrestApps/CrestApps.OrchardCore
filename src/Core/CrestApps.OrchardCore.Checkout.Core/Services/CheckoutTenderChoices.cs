using CrestApps.OrchardCore.Checkout.Services;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Checkout.Core.Services;

/// <summary>
/// Chooses the balances that pay toward a checkout, such as the buyer's store credit or points, and rebuilds the
/// invoice so the buyer sees what's left to pay. The Payment step's boxes and the commerce API both go through here.
/// </summary>
public sealed class CheckoutTenderChoices
{
    private readonly CheckoutInvoiceBuilder _invoiceBuilder;

    public CheckoutTenderChoices(CheckoutInvoiceBuilder invoiceBuilder)
    {
        _invoiceBuilder = invoiceBuilder;
    }

    /// <summary>
    /// Returns whether the checkout takes balances: not one that settles an existing balance, nor one that saves a
    /// card to charge later.
    /// </summary>
    public static bool AcceptsTenders(CheckoutFlow flow)
        => flow?.Session is CheckoutSession { SettlesExistingBalance: false, SavePaymentMethod: false };

    /// <summary>
    /// Adds a balance, and rebuilds the invoice. Returns whether it pays anything toward the order.
    /// </summary>
    public async Task<bool> AddAsync(CheckoutFlow flow, string providerKey, string reference)
    {
        ArgumentNullException.ThrowIfNull(flow);

        if (!AcceptsTenders(flow) || string.IsNullOrEmpty(reference))
        {
            return false;
        }

        var selection = flow.Session.TryGet<CheckoutTenderSelection>(out var existing) ? existing : new CheckoutTenderSelection();

        selection.Add(providerKey, reference);
        flow.Session.Put(selection);

        await _invoiceBuilder.BuildAsync(flow);

        return flow.Session.TryGet<CheckoutInvoice>(out var invoice) &&
            (invoice.Tenders ?? []).Any(tender => tender.ProviderKey == providerKey && tender.Reference == reference);
    }

    /// <summary>
    /// Takes a balance off, and rebuilds the invoice. Returns whether it was chosen.
    /// </summary>
    public async Task<bool> RemoveAsync(CheckoutFlow flow, string providerKey, string reference)
    {
        ArgumentNullException.ThrowIfNull(flow);

        if (!flow.Session.TryGet<CheckoutTenderSelection>(out var selection) || !selection.Remove(providerKey, reference))
        {
            return false;
        }

        flow.Session.Put(selection);
        await _invoiceBuilder.BuildAsync(flow);

        return true;
    }
}
