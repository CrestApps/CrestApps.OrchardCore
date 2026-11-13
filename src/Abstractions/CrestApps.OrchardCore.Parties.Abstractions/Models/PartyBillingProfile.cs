using CrestApps.OrchardCore.Addresses.Models;

namespace CrestApps.OrchardCore.Parties.Models;

/// <summary>
/// What a business document billed to a party needs to know about it: who to address, where, its tax details, the
/// currency and language it is billed in, and its terms. The module that keeps each kind of party fills it through an
/// <see cref="Services.IPartyBillingProfileSource"/>, and <see cref="Services.IPartyBillingProfileService"/> reads it.
/// A document keeps a copy, so a later change to the party never changes an issued document.
/// </summary>
public sealed class PartyBillingProfile
{
    /// <summary>
    /// Gets or sets the party the profile is of.
    /// </summary>
    public PartyRef Party { get; set; }

    /// <summary>
    /// Gets or sets who to address and where: the party's name, e-mail, phone and billing address. It is never
    /// <see langword="null"/>; a detail the party has none of is empty.
    /// </summary>
    public ContactInfo Contact { get; set; } = new();

    /// <summary>
    /// Gets or sets where goods for the party go, or <see langword="null"/> when it is the billing address.
    /// </summary>
    public Address ShippingAddress { get; set; }

    /// <summary>
    /// Gets or sets the party's tax or VAT number, or <see langword="null"/>.
    /// </summary>
    public string TaxId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the party pays no tax, such as a reseller or a charity.
    /// </summary>
    public bool TaxExempt { get; set; }

    /// <summary>
    /// Gets or sets the ISO 4217 code of the currency the party is billed in, or <see langword="null"/> for the site's.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the culture the party's documents are written in, such as <c>fr-FR</c>, or <see langword="null"/>
    /// for the site's.
    /// </summary>
    public string Culture { get; set; }

    /// <summary>
    /// Gets or sets the id of the payment term record the party pays on, or <see langword="null"/> when none is set.
    /// </summary>
    public string PaymentTermId { get; set; }

    /// <summary>
    /// Gets or sets the days a document is due in, for an owner that keeps the party's terms as a day count only, or
    /// <see langword="null"/>.
    /// </summary>
    public int? NetDays { get; set; }

    /// <summary>
    /// Gets or sets the most the party may owe at once, or <see langword="null"/> for no limit.
    /// </summary>
    public decimal? CreditLimit { get; set; }

    /// <summary>
    /// Gets or sets the extra e-mail addresses that get a copy of the party's billing documents.
    /// </summary>
    public IList<string> CopyTo { get; set; } = [];

    /// <summary>
    /// Adds an e-mail address that gets a copy of the party's billing documents. A blank address, one already listed
    /// and the contact's own address are left out. Addresses compare without case.
    /// </summary>
    /// <param name="email">The e-mail address.</param>
    /// <returns><see langword="true"/> when the address was added.</returns>
    public bool AddCopyTo(string email)
    {
        var value = email?.Trim();

        if (string.IsNullOrEmpty(value) ||
            string.Equals(value, Contact?.Email, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        CopyTo ??= [];

        if (CopyTo.Any(existing => string.Equals(existing, value, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        CopyTo.Add(value);

        return true;
    }
}
