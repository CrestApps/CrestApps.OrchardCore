using CrestApps.OrchardCore.Checkout.Models;

namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// A kind of balance a buyer can pay part or all of a checkout with, such as gift cards or store credit.
/// </summary>
/// <remarks>
/// A provider only says which balances the buyer chose and how much is on them, and moves money on and off them.
/// <see cref="ICheckoutTenderService"/> decides how much each pays, and <see cref="ICheckoutTenderLedger"/> holds it
/// while the buyer pays the rest, so two checkouts can't spend the same money.
/// </remarks>
public interface ICheckoutTenderProvider
{
    /// <summary>
    /// Gets the provider's stable key, recorded on every <see cref="TenderLine"/> it gives.
    /// </summary>
    string Key { get; }

    /// <summary>
    /// Gets whether the provider's balances can pay toward a checkout that keeps the card to charge later
    /// (<see cref="CheckoutSession.SavePaymentMethod"/>), such as a balance owed later that the kept card is charged for.
    /// Other balances can't, since a checkout they paid in full would never see the card.
    /// </summary>
    bool PaysWithSavedPaymentMethod
        => false;

    /// <summary>
    /// Returns a line for each of the provider's balances the buyer chose for the checkout, with
    /// <see cref="TenderLine.Amount"/> set to everything on it. A balance in another currency, or one that can't pay
    /// this checkout, is left out.
    /// </summary>
    Task<IReadOnlyList<TenderLine>> GetTendersAsync(CheckoutTenderContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns what is on a balance now in <paramref name="currency"/>, or <see langword="null"/> when it can't pay at
    /// all (disabled, expired, or in another currency).
    /// </summary>
    Task<decimal?> GetBalanceAsync(string reference, string currency, CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes <paramref name="amount"/> off a balance, for the checkout that paid with it. Called once per checkout,
    /// under the balance's lock.
    /// </summary>
    Task DebitAsync(string reference, decimal amount, string currency, CheckoutFlow flow, CancellationToken cancellationToken = default);

    /// <summary>
    /// Puts <paramref name="amount"/> back on a balance, such as for a refund. Called under the balance's lock.
    /// </summary>
    Task CreditAsync(string reference, decimal amount, string currency, string reason, CancellationToken cancellationToken = default);
}

/// <summary>
/// What a <see cref="ICheckoutTenderProvider"/> is asked about.
/// </summary>
public sealed class CheckoutTenderContext
{
    public CheckoutTenderContext(CheckoutInvoice invoice, CheckoutFlow flow, IReadOnlyList<string> references)
    {
        Invoice = invoice;
        Flow = flow;
        References = references;
    }

    /// <summary>
    /// Gets the invoice, with tax already worked out.
    /// </summary>
    public CheckoutInvoice Invoice { get; }

    /// <summary>
    /// Gets the checkout.
    /// </summary>
    public CheckoutFlow Flow { get; }

    /// <summary>
    /// Gets the provider's balances the buyer chose, in the order they chose them.
    /// </summary>
    public IReadOnlyList<string> References { get; }
}

/// <summary>
/// Applies the balances a buyer chose to the checkout's invoice, after tax.
/// </summary>
public interface ICheckoutTenderService
{
    /// <summary>
    /// Fills <see cref="CheckoutInvoice.Tenders"/> and takes what they pay off
    /// <see cref="CheckoutInvoice.InitialPaymentAmount"/>.
    /// </summary>
    Task ApplyTendersAsync(CheckoutInvoice invoice, CheckoutFlow flow, CancellationToken cancellationToken = default);
}

/// <summary>
/// The balances a buyer chose to pay part of a checkout with, stored on the checkout like the codes it carries.
/// </summary>
public sealed class CheckoutTenderSelection
{
    /// <summary>
    /// Gets or sets the balances, in the order the buyer chose them.
    /// </summary>
    public List<CheckoutTenderChoice> Choices { get; set; } = [];

    /// <summary>
    /// Adds a balance, unless it's chosen already.
    /// </summary>
    public void Add(string providerKey, string reference)
    {
        Choices ??= [];

        if (!Choices.Any(choice => choice.Is(providerKey, reference)))
        {
            Choices.Add(new CheckoutTenderChoice { ProviderKey = providerKey, Reference = reference });
        }
    }

    /// <summary>
    /// Takes a balance off. Returns whether it was chosen.
    /// </summary>
    public bool Remove(string providerKey, string reference)
        => (Choices?.RemoveAll(choice => choice.Is(providerKey, reference)) ?? 0) > 0;

    /// <summary>
    /// Returns the references chosen with one provider, in order.
    /// </summary>
    public IReadOnlyList<string> For(string providerKey)
        => [.. (Choices ?? []).Where(choice => string.Equals(choice.ProviderKey, providerKey, StringComparison.Ordinal)).Select(choice => choice.Reference)];
}

/// <summary>
/// One balance a buyer chose.
/// </summary>
public sealed class CheckoutTenderChoice
{
    public string ProviderKey { get; set; }

    public string Reference { get; set; }

    public bool Is(string providerKey, string reference)
        => string.Equals(ProviderKey, providerKey, StringComparison.Ordinal) &&
            string.Equals(Reference, reference, StringComparison.Ordinal);
}
