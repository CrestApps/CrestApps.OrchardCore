using CrestApps.OrchardCore.Addresses.Core.Editing;
using CrestApps.OrchardCore.Accounts.B2B.Models;
using CrestApps.OrchardCore.Orders.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;

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

    public List<AccountBuyerRowViewModel> Buyers { get; set; } = [];

    public List<AccountLocationRowViewModel> Locations { get; set; } = [];

    [BindNever]
    public string StoreCurrency { get; set; }

    [BindNever]
    public IList<SelectListItem> PriceLists { get; set; } = [];

    public bool PunchoutEnabled { get; set; }

    public string PunchoutUser { get; set; }

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

public class AccountBuyerRowViewModel
{
    /// <summary>
    /// Gets or sets the buyer's user name or email.
    /// </summary>
    public string User { get; set; }

    public AccountBuyerRole Role { get; set; }

    public decimal? OrderLimit { get; set; }
}

public class AccountLocationRowViewModel
{
    public string LocationId { get; set; }

    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the address as the editor posts it, under the row's <c>Address</c> prefix.
    /// </summary>
    public AddressEditorViewModel Address { get; set; } = new();

    /// <summary>
    /// Gets or sets the address editor, laid out for the address's country.
    /// </summary>
    [BindNever]
    public IShape AddressEditor { get; set; }
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
