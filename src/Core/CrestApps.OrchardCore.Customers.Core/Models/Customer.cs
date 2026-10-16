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
    /// Gets or sets the email the store writes to. For a customer with an account, it is the account's email.
    /// </summary>
    public string Email { get; set; }

    public string Phone { get; set; }

    /// <summary>
    /// Gets or sets whether the customer agreed to receive marketing. Order and payment messages are always sent.
    /// </summary>
    public bool AcceptsMarketing { get; set; }

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
}
