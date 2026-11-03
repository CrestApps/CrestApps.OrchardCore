using CrestApps.OrchardCore.Addresses.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Accounts.B2B.Models;

/// <summary>
/// Lets an account buy from the store as a business: the people who buy for it, the places it ships to, the terms it
/// buys on and the prices it pays. The account's name is its title.
/// </summary>
public sealed class B2BAccountPart : ContentPart
{
    /// <summary>
    /// Gets or sets whether the account buys as a business. While it is off, its buyers shop as themselves.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets its tax or VAT number, printed on its invoices.
    /// </summary>
    public string TaxId { get; set; }

    /// <summary>
    /// Gets or sets where its invoices are sent, besides to the buyer.
    /// </summary>
    public string BillingEmail { get; set; }

    public List<AccountBuyer> Buyers { get; set; } = [];

    public List<AccountLocation> Locations { get; set; } = [];

    public AccountTerms Terms { get; set; } = new();

    /// <summary>
    /// Gets or sets the price list its buyers pay, or none for the store's prices.
    /// </summary>
    public string PriceListId { get; set; }

    /// <summary>
    /// Gets or sets how much its buyers may order in a calendar month, in the store's currency; orders beyond it need
    /// an account admin's approval. None is no budget.
    /// </summary>
    public decimal? MonthlyBudget { get; set; }

    /// <summary>
    /// Gets or sets how the account's procurement system shops the store by OCI punchout, or none.
    /// </summary>
    public AccountPunchout Punchout { get; set; }
}

/// <summary>
/// An account that buys as a business: the account content item with its <see cref="B2BAccountPart"/>, read together.
/// </summary>
public sealed class B2BAccount
{
    /// <summary>
    /// Initializes a new instance of the <see cref="B2BAccount"/> class.
    /// </summary>
    /// <param name="contentItem">The account content item.</param>
    public B2BAccount(ContentItem contentItem)
    {
        ArgumentNullException.ThrowIfNull(contentItem);

        ContentItem = contentItem;
        Part = contentItem.TryGet<B2BAccountPart>(out var part) ? part : new B2BAccountPart();
    }

    /// <summary>
    /// Gets the account content item.
    /// </summary>
    public ContentItem ContentItem { get; }

    /// <summary>
    /// Gets the account's business settings.
    /// </summary>
    public B2BAccountPart Part { get; }

    /// <summary>
    /// Gets the account's content item id, which orders, invoices and punchout links refer to it by.
    /// </summary>
    public string AccountId
        => ContentItem.ContentItemId;

    /// <summary>
    /// Gets the account's name.
    /// </summary>
    public string Name
        => ContentItem.DisplayText;

    /// <summary>
    /// Gets whether the account buys as a business: its part is on and the account is published.
    /// </summary>
    public bool IsEnabled
        => Part.Enabled && ContentItem.Published;

    public string TaxId
        => Part.TaxId;

    public string BillingEmail
        => Part.BillingEmail;

    public IReadOnlyList<AccountBuyer> Buyers
        => Part.Buyers ?? [];

    public IReadOnlyList<AccountLocation> Locations
        => Part.Locations ?? [];

    public AccountTerms Terms
        => Part.Terms ?? new AccountTerms();

    public string PriceListId
        => Part.PriceListId;

    public decimal? MonthlyBudget
        => Part.MonthlyBudget;

    public AccountPunchout Punchout
        => Part.Punchout;

    /// <summary>
    /// Returns the buyer a user is, or <see langword="null"/>.
    /// </summary>
    public AccountBuyer FindBuyer(string userId)
        => string.IsNullOrEmpty(userId) ? null : Buyers.FirstOrDefault(buyer => string.Equals(buyer.UserId, userId, StringComparison.Ordinal));
}

/// <summary>
/// Someone who buys for an account: a buyer, or an admin who also manages the account's buyers and approves orders.
/// </summary>
public sealed class AccountBuyer
{
    public string UserId { get; set; }

    public string UserName { get; set; }

    public string Email { get; set; }

    public AccountBuyerRole Role { get; set; }

    /// <summary>
    /// Gets or sets the most an order the buyer places may come to before an account admin has to approve it, or none.
    /// </summary>
    public decimal? OrderLimit { get; set; }

    /// <summary>
    /// Gets or sets the account's contact this buyer is, matched by email, or none.
    /// </summary>
    public string ContactContentItemId { get; set; }
}

/// <summary>
/// An account's OCI punchout: its procurement system opens the store with the key, as the punchout buyer.
/// </summary>
public sealed class AccountPunchout
{
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the user the procurement system shops as: one of the account's buyers.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the SHA-256 hash of the key, as hex. The key itself is shown once, when it's made.
    /// </summary>
    public string KeyHash { get; set; }

    public DateTime? KeyCreatedUtc { get; set; }
}

public enum AccountBuyerRole
{
    Buyer,

    /// <summary>
    /// Sees all of the account's orders, and approves orders over a buyer's limit or the account's budget.
    /// </summary>
    Admin,
}

/// <summary>
/// A place an account has goods shipped to, offered to its buyers at checkout.
/// </summary>
public sealed class AccountLocation
{
    public string LocationId { get; set; }

    public string Name { get; set; }

    public Address Address { get; set; }
}

/// <summary>
/// How an account pays: on account, with a deposit paid at checkout and the rest invoiced, due in its net days.
/// </summary>
public sealed class AccountTerms
{
    /// <summary>
    /// Gets or sets the days the invoice for what's owed is due in, such as 30; none means the account pays at checkout
    /// like anyone else.
    /// </summary>
    public int? NetDays { get; set; }

    /// <summary>
    /// Gets or sets the part of an order, from 0 to 100 percent, paid at checkout before the rest is invoiced.
    /// </summary>
    public decimal DepositPercent { get; set; }

    /// <summary>
    /// Gets or sets the most the account may owe at once, in the store's currency, or none for no limit.
    /// </summary>
    public decimal? CreditLimit { get; set; }

    /// <summary>
    /// Gets or sets whether its buyers have to give a purchase order number at checkout.
    /// </summary>
    public bool RequirePurchaseOrder { get; set; }

    public bool OffersTerms
        => NetDays is >= 0;
}
