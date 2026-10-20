using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Payments;
using Microsoft.Extensions.Logging;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Checkout.Core.Services;

/// <summary>
/// The default <see cref="ICheckoutTenderService"/>. Providers say which balances the buyer chose and what is on
/// them; this decides how much each pays.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description>Balances pay after tax, toward the one-time amount charged now, in the order the buyer chose
/// them, each up to what is left on it once other checkouts' holds are counted.</description></item>
/// <item><description>They never pay a recurring charge: the agreement the payment provider sets up is for the full
/// cycle amount.</description></item>
/// <item><description>The payment provider carries the tax first, so the payment it reports still splits into the
/// amount and the tax the invoice expects; a balance carries only the tax left over.</description></item>
/// </list>
/// </remarks>
public sealed class DefaultCheckoutTenderService : ICheckoutTenderService
{
    private readonly IEnumerable<ICheckoutTenderProvider> _providers;
    private readonly ICheckoutTenderLedger _ledger;
    private readonly ILogger _logger;

    public DefaultCheckoutTenderService(
        IEnumerable<ICheckoutTenderProvider> providers,
        ICheckoutTenderLedger ledger,
        ILogger<DefaultCheckoutTenderService> logger)
    {
        _providers = providers;
        _ledger = ledger;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task ApplyTendersAsync(CheckoutInvoice invoice, CheckoutFlow flow, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        invoice.Tenders ??= [];
        invoice.Tenders.Clear();

        // A checkout that settles an existing balance pays with the card alone, and one that saves a card to charge later
        // takes only the balances that card is charged for later.
        if (flow?.Session is not CheckoutSession session ||
            session.SettlesExistingBalance ||
            !session.TryGet<CheckoutTenderSelection>(out var selection) ||
            selection.Choices is not { Count: > 0 })
        {
            return;
        }

        var initial = invoice.InitialPaymentAmount ?? 0m;

        if (initial <= 0m)
        {
            return;
        }

        var offered = new Dictionary<(string, string), TenderLine>();

        foreach (var provider in _providers)
        {
            var references = selection.For(provider.Key);

            if (references.Count == 0 || (session.SavePaymentMethod && !provider.PaysWithSavedPaymentMethod))
            {
                continue;
            }

            try
            {
                foreach (var line in await provider.GetTendersAsync(new CheckoutTenderContext(invoice, flow, references), cancellationToken) ?? [])
                {
                    line.ProviderKey = provider.Key;
                    offered[(provider.Key, line.Reference)] = line;
                }
            }
            catch (Exception exception)
            {
                // The buyer pays the rest by card rather than being stopped from buying.
                _logger.LogError(exception, "The tender provider '{ProviderKey}' failed. The checkout continues without its balances.", provider.Key);
            }
        }

        var remaining = initial;

        foreach (var choice in selection.Choices)
        {
            if (remaining <= 0m || !offered.TryGetValue((choice.ProviderKey, choice.Reference), out var line))
            {
                continue;
            }

            var available = line.Amount - await _ledger.GetHeldByOthersAsync(line.ProviderKey, line.Reference, session.SessionId, cancellationToken);
            var amount = Money.Round(Math.Min(available, remaining), invoice.Currency);

            if (amount <= 0m)
            {
                continue;
            }

            line.Amount = amount;
            line.TaxAmount = 0m;
            invoice.Tenders.Add(line);
            remaining = Money.Round(remaining - amount, invoice.Currency);
        }

        if (invoice.Tenders.Count == 0)
        {
            return;
        }

        invoice.InitialPaymentAmount = remaining;

        // The card pays the tax first; whatever tax it can't cover, because the balances paid nearly everything, is
        // spread over the balances in proportion to what they pay.
        var tenderTax = Money.Round(Math.Max(0m, invoice.TaxAmount - remaining), invoice.Currency);
        var total = invoice.TenderTotal;
        var left = tenderTax;

        for (var i = 0; i < invoice.Tenders.Count && tenderTax > 0m; i++)
        {
            var share = i == invoice.Tenders.Count - 1
                ? left
                : Money.Round(tenderTax * (invoice.Tenders[i].Amount / total), invoice.Currency);

            share = Math.Min(share, Math.Min(left, invoice.Tenders[i].Amount));
            invoice.Tenders[i].TaxAmount = share;
            left -= share;
        }
    }
}
