using CrestApps.OrchardCore.Companies.Models;
using CrestApps.OrchardCore.Orders.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Companies.ViewModels;

public class CompaniesIndexViewModel
{
    public AdminListContext<Company> List { get; set; }

    public string Search { get; set; }
}

public class CompanyEditViewModel
{
    [BindNever]
    public string ItemId { get; set; }

    public bool IsNew
        => string.IsNullOrEmpty(ItemId);

    public string Name { get; set; }

    public string TaxId { get; set; }

    public string BillingEmail { get; set; }

    public bool IsEnabled { get; set; } = true;

    public int? NetDays { get; set; }

    public decimal DepositPercent { get; set; }

    public decimal? CreditLimit { get; set; }

    public bool RequirePurchaseOrder { get; set; }

    public decimal? MonthlyBudget { get; set; }

    public string PriceListId { get; set; }

    public List<CompanyBuyerRowViewModel> Buyers { get; set; } = [];

    public List<CompanyLocationRowViewModel> Locations { get; set; } = [];

    [BindNever]
    public string StoreCurrency { get; set; }

    [BindNever]
    public IList<SelectListItem> PriceLists { get; set; } = [];
}

public class CompanyBuyerRowViewModel
{
    /// <summary>
    /// Gets or sets the buyer's user name or email.
    /// </summary>
    public string User { get; set; }

    public CompanyBuyerRole Role { get; set; }

    public decimal? OrderLimit { get; set; }
}

public class CompanyLocationRowViewModel
{
    public string LocationId { get; set; }

    public string Name { get; set; }

    public string AddressLine1 { get; set; }

    public string City { get; set; }

    public string Region { get; set; }

    public string PostalCode { get; set; }

    public string Country { get; set; }
}

/// <summary>
/// The Company page of a buyer's storefront account.
/// </summary>
public class CompanyAccountViewModel
{
    public Company Company { get; set; }

    public CompanyBuyer Buyer { get; set; }

    public IReadOnlyList<Order> Orders { get; set; } = [];

    public decimal Owed { get; set; }

    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the orders waiting for an admin's approval, for an admin.
    /// </summary>
    public IReadOnlyList<Order> Approvals { get; set; } = [];

    /// <summary>
    /// Gets or sets what the company's paid orders came to this month, when it has a monthly budget.
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
