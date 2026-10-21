using CrestApps.Core.Models;
using CrestApps.OrchardCore.Addresses.Models;

namespace CrestApps.OrchardCore.Companies.Models;

/// <summary>
/// A business the store sells to: the people who buy for it, the places it ships to, and the terms it buys on.
/// </summary>
public sealed class Company : CatalogItem
{
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets its tax or VAT number, printed on its invoices.
    /// </summary>
    public string TaxId { get; set; }

    /// <summary>
    /// Gets or sets where its invoices are sent, besides to the buyer.
    /// </summary>
    public string BillingEmail { get; set; }

    public List<CompanyBuyer> Buyers { get; set; } = [];

    public List<CompanyLocation> Locations { get; set; } = [];

    public CompanyTerms Terms { get; set; } = new();

    /// <summary>
    /// Gets or sets the price list its buyers pay, or none for the store's prices.
    /// </summary>
    public string PriceListId { get; set; }

    /// <summary>
    /// Gets or sets how much its buyers may order in a calendar month, in the store's currency; orders beyond it need a
    /// company admin's approval. None is no budget.
    /// </summary>
    public decimal? MonthlyBudget { get; set; }

    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets how the company's procurement system shops the store by OCI punchout, or none.
    /// </summary>
    public CompanyPunchout Punchout { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Returns the buyer a user is, or <see langword="null"/>.
    /// </summary>
    public CompanyBuyer FindBuyer(string userId)
        => string.IsNullOrEmpty(userId) ? null : Buyers.FirstOrDefault(buyer => string.Equals(buyer.UserId, userId, StringComparison.Ordinal));
}

/// <summary>
/// Someone who buys for a company: a buyer, or an admin who also manages the company's buyers and approves orders.
/// </summary>
public sealed class CompanyBuyer
{
    public string UserId { get; set; }

    public string UserName { get; set; }

    public string Email { get; set; }

    public CompanyBuyerRole Role { get; set; }

    /// <summary>
    /// Gets or sets the most an order the buyer places may come to before a company admin has to approve it, or none.
    /// </summary>
    public decimal? OrderLimit { get; set; }
}

/// <summary>
/// A company's OCI punchout: its procurement system opens the store with the key, as the punchout buyer.
/// </summary>
public sealed class CompanyPunchout
{
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the user the procurement system shops as: one of the company's buyers.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the SHA-256 hash of the key, as hex. The key itself is shown once, when it's made.
    /// </summary>
    public string KeyHash { get; set; }

    public DateTime? KeyCreatedUtc { get; set; }
}

public enum CompanyBuyerRole
{
    Buyer,

    /// <summary>
    /// Sees all of the company's orders, and approves orders over a buyer's limit or the company's budget.
    /// </summary>
    Admin,
}

/// <summary>
/// A place a company has goods shipped to, offered to its buyers at checkout.
/// </summary>
public sealed class CompanyLocation
{
    public string LocationId { get; set; }

    public string Name { get; set; }

    public Address Address { get; set; }
}

/// <summary>
/// How a company pays: on account, with a deposit paid at checkout and the rest invoiced, due in its net days.
/// </summary>
public sealed class CompanyTerms
{
    /// <summary>
    /// Gets or sets the days the invoice for what's owed is due in, such as 30; none means the company pays at checkout
    /// like anyone else.
    /// </summary>
    public int? NetDays { get; set; }

    /// <summary>
    /// Gets or sets the part of an order, from 0 to 100 percent, paid at checkout before the rest is invoiced.
    /// </summary>
    public decimal DepositPercent { get; set; }

    /// <summary>
    /// Gets or sets the most the company may owe at once, in the store's currency, or none for no limit.
    /// </summary>
    public decimal? CreditLimit { get; set; }

    /// <summary>
    /// Gets or sets whether its buyers have to give a purchase order number at checkout.
    /// </summary>
    public bool RequirePurchaseOrder { get; set; }

    public bool OffersTerms
        => NetDays is >= 0;
}
