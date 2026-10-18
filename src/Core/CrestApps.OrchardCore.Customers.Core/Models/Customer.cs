using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Customers.Core.Models;

/// <summary>
/// Someone who buys from the store: a person or a business, with an account or as a guest.
/// </summary>
/// <remarks>
/// A customer with an account is linked to its Orchard Core user, which keeps the sign-in, password and email
/// confirmation. The customer keeps what the store needs: how to address them, their company, how they want to be
/// contacted, and (later) their addresses. Orders, transactions and subscriptions point at the customer, so they
/// stay together whatever happens to the account.
/// </remarks>
public sealed class Customer : CatalogItem
{
    public long Id { get; set; }

    public CustomerKind Kind { get; set; }

    public CustomerStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the Orchard Core user of a customer with an account, or <see langword="null"/> for a guest.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the name the store addresses the customer by.
    /// </summary>
    public string DisplayName { get; set; }

    public string FirstName { get; set; }

    public string LastName { get; set; }

    /// <summary>
    /// Gets or sets the company of a business customer.
    /// </summary>
    public string CompanyName { get; set; }

    /// <summary>
    /// Gets or sets the tax id of a business customer, such as a VAT number.
    /// </summary>
    public string TaxId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the customer pays no tax, such as a reseller or a charity the store
    /// holds a certificate for.
    /// </summary>
    public bool IsTaxExempt { get; set; }

    /// <summary>
    /// Gets or sets why the customer is exempt, such as a resale certificate number, for the store's records.
    /// </summary>
    public string TaxExemptionReason { get; set; }

    /// <summary>
    /// Gets or sets the email the store writes to. For a customer with an account, it is the account's email.
    /// </summary>
    public string Email { get; set; }

    public string Phone { get; set; }

    /// <summary>
    /// Gets or sets whether the customer agreed to receive marketing. Order and payment messages are always sent.
    /// </summary>
    public bool AcceptsMarketing { get; set; }

    /// <summary>
    /// Gets or sets the customer's address book.
    /// </summary>
    public List<CustomerAddress> Addresses { get; set; } = [];

    /// <summary>
    /// Gets or sets the address billed first, from <see cref="Addresses"/>.
    /// </summary>
    public string DefaultBillingAddressId { get; set; }

    /// <summary>
    /// Gets or sets the address shipped to first, from <see cref="Addresses"/>.
    /// </summary>
    public string DefaultShippingAddressId { get; set; }

    /// <summary>
    /// Gets or sets the customer's ids at outside services, such as a payment provider's customer id, by service.
    /// </summary>
    public Dictionary<string, string> ExternalIds { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public DateTime CreatedUtc { get; set; }

    public DateTime ModifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the customer last bought or changed something, which decides when their data is kept until.
    /// </summary>
    public DateTime LastActivityUtc { get; set; }

    /// <summary>
    /// Gets or sets the customer a merged guest's history moved to.
    /// </summary>
    public string MergedIntoCustomerId { get; set; }

    /// <summary>
    /// Gets or sets when the guest was merged.
    /// </summary>
    public DateTime? MergedUtc { get; set; }

    /// <summary>
    /// Gets or sets the guest customers merged into this one.
    /// </summary>
    public List<string> MergedCustomerIds { get; set; } = [];

    /// <summary>
    /// Gets or sets when the customer's personal data was erased.
    /// </summary>
    public DateTime? AnonymizedUtc { get; set; }

    /// <summary>
    /// Gets or sets what was done with the customer's personal data, and by whom: exports and erasures.
    /// </summary>
    public List<CustomerPrivacyEntry> PrivacyLog { get; set; } = [];

    /// <summary>
    /// Removes everything personal: names, company, tax id, email, phone, addresses, outside ids and marketing consent.
    /// The record stays, anonymized, so the orders that name it still add up. An account's customer is also unlinked
    /// from the account, so the user starts a new customer if they buy again.
    /// </summary>
    public void Anonymize(DateTime now)
    {
        DisplayName = null;
        FirstName = null;
        LastName = null;
        CompanyName = null;
        TaxId = null;
        TaxExemptionReason = null;
        Email = null;
        Phone = null;
        AcceptsMarketing = false;
        Addresses = [];
        DefaultBillingAddressId = null;
        DefaultShippingAddressId = null;
        ExternalIds?.Clear();
        UserId = null;
        Status = CustomerStatus.Anonymized;
        AnonymizedUtc ??= now;
        ModifiedUtc = now;
    }

    public CustomerAddress FindAddress(string addressId)
        => string.IsNullOrEmpty(addressId)
            ? null
            : Addresses?.FirstOrDefault(address => string.Equals(address.AddressId, addressId, StringComparison.Ordinal));

    public CustomerAddress GetDefaultBillingAddress()
        => FindAddress(DefaultBillingAddressId);

    public CustomerAddress GetDefaultShippingAddress()
        => FindAddress(DefaultShippingAddressId);

    /// <summary>
    /// Removes an address, and the defaults that named it. Returns false when there is no such address.
    /// </summary>
    public bool RemoveAddress(string addressId)
    {
        if (Addresses is null || Addresses.RemoveAll(address => string.Equals(address.AddressId, addressId, StringComparison.Ordinal)) == 0)
        {
            return false;
        }

        if (string.Equals(DefaultBillingAddressId, addressId, StringComparison.Ordinal))
        {
            DefaultBillingAddressId = null;
        }

        if (string.Equals(DefaultShippingAddressId, addressId, StringComparison.Ordinal))
        {
            DefaultShippingAddressId = null;
        }

        return true;
    }

    /// <summary>
    /// The name to show: the display name, or the person's or company's name, or the email.
    /// </summary>
    public string GetName()
    {
        if (!string.IsNullOrWhiteSpace(DisplayName))
        {
            return DisplayName;
        }

        var personName = string.Join(' ', new[] { FirstName, LastName }.Where(part => !string.IsNullOrWhiteSpace(part)));

        if (Kind == CustomerKind.Business && !string.IsNullOrWhiteSpace(CompanyName))
        {
            return CompanyName;
        }

        return string.IsNullOrWhiteSpace(personName) ? Email : personName;
    }
}

public enum CustomerKind
{
    Person = 0,

    Business = 1,
}

public enum CustomerStatus
{
    Active = 0,

    /// <summary>
    /// The store stopped doing business with the customer. Their orders and history stay.
    /// </summary>
    Disabled = 1,

    /// <summary>
    /// The customer's personal data was erased; only what the law requires to keep remains.
    /// </summary>
    Anonymized = 2,

    /// <summary>
    /// A guest customer whose orders and addresses moved to an account's customer when that account's email was
    /// verified. <see cref="Customer.MergedIntoCustomerId"/> names it.
    /// </summary>
    Merged = 3,
}
