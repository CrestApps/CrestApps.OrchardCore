using CrestApps.OrchardCore.Accounts.B2B.Models;
using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Orders.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Accounts.B2B.ViewModels;

/// <summary>
/// The B2B settings on an account's editor.
/// </summary>
public class B2BAccountPartViewModel
{
    [BindNever]
    public string AccountId { get; set; }

    public bool Enabled { get; set; }

    public string TaxId { get; set; }

    public string BillingEmail { get; set; }

    public int? NetDays { get; set; }

    public decimal DepositPercent { get; set; }

    public decimal? CreditLimit { get; set; }

    public bool RequirePurchaseOrder { get; set; }

    public decimal? MonthlyBudget { get; set; }

    public string PriceListId { get; set; }

    /// <summary>
    /// Gets or sets the order limit of each of the account's members.
    /// </summary>
    public List<BuyerOrderLimitRowViewModel> OrderLimits { get; set; } = [];

    [BindNever]
    public string StoreCurrency { get; set; }

    [BindNever]
    public IList<SelectListItem> PriceLists { get; set; } = [];

    public bool PunchoutEnabled { get; set; }

    /// <summary>
    /// Gets or sets the user id of the member the procurement system shops as.
    /// </summary>
    public string PunchoutUserId { get; set; }

    /// <summary>
    /// Gets or sets the account's members, to choose the punchout buyer from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> Members { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the procurement system may open the store with a link (OCI link mode).
    /// </summary>
    public bool PunchoutAllowLinkSignIn { get; set; }

    public bool NewPunchoutKey { get; set; }

    [BindNever]
    public DateTime? PunchoutKeyCreatedUtc { get; set; }

    [BindNever]
    public string PunchoutUrl { get; set; }

    [BindNever]
    public string Prefix { get; set; }
}

public class PunchoutTransferViewModel
{
    public string HookUrl { get; set; }

    public string Target { get; set; }

    public IReadOnlyList<KeyValuePair<string, string>> Fields { get; set; } = [];

    public int ItemCount { get; set; }
}

/// <summary>
/// One member's order limit on an account's editor.
/// </summary>
public class BuyerOrderLimitRowViewModel
{
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the member's email or user name.
    /// </summary>
    [BindNever]
    public string User { get; set; }

    [BindNever]
    public AccountMemberRole Role { get; set; }

    public decimal? Limit { get; set; }
}

/// <summary>
/// The Business account page of a buyer's storefront account.
/// </summary>
public class BusinessAccountViewModel
{
    public B2BAccount Account { get; set; }

    public AccountBuyer Buyer { get; set; }

    public IReadOnlyList<Order> Orders { get; set; } = [];

    public decimal Owed { get; set; }

    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the orders waiting for an admin's approval, for an admin.
    /// </summary>
    public IReadOnlyList<Order> Approvals { get; set; } = [];

    /// <summary>
    /// Gets or sets what the account's paid orders came to this month, when it has a monthly budget.
    /// </summary>
    public decimal? SpentThisMonth { get; set; }
}

public class PriceListsIndexViewModel
{
    public AdminListContext<PriceList> List { get; set; }

    public string Search { get; set; }
}

public class PriceListEditViewModel
{
    [BindNever]
    public string ItemId { get; set; }

    public bool IsNew
        => string.IsNullOrEmpty(ItemId);

    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the percentage off, as text, so a page in a language with a decimal comma reads "12.5" right.
    /// </summary>
    public string PercentOff { get; set; }

    /// <summary>
    /// Gets or sets the prices by SKU: a SKU and a price on each line.
    /// </summary>
    public string Prices { get; set; }

    [BindNever]
    public string StoreCurrency { get; set; }
}
