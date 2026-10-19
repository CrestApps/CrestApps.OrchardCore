using CrestApps.OrchardCore.Addresses.Core.Editing;
using CrestApps.OrchardCore.Customers.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Customers.ViewModels;

public class CustomersIndexViewModel
{
    public CustomersIndexOptions Options { get; set; } = new();

    public IReadOnlyList<Customer> Customers { get; set; } = [];

    public int TotalCount { get; set; }

    public bool CanManage { get; set; }

    [BindNever]
    public IShape Pager { get; set; }

    /// <summary>
    /// Gets or sets the customers, pager and region shapes the <c>admin-list</c> tag renders, in the
    /// layout (List or Grid) the site or the user picked.
    /// </summary>
    [BindNever]
    public AdminListContext List { get; set; }
}

public class CustomersIndexOptions
{
    public string Search { get; set; }

    public CustomerListFilter Filter { get; set; }

    [BindNever]
    public List<SelectListItem> Filters { get; set; } = [];
}

public enum CustomerListFilter
{
    Active,

    WithAccount,

    Guests,

    Businesses,

    Disabled,

    All,
}

public class CustomerEditViewModel
{
    [BindNever]
    public string CustomerId { get; set; }

    /// <summary>
    /// Gets or sets what other features add to the customer's page, such as their store credit.
    /// </summary>
    [BindNever]
    public global::OrchardCore.DisplayManagement.IShape Sections { get; set; }

    [BindNever]
    public bool IsNew { get; set; }

    [BindNever]
    public bool CanManage { get; set; }

    [BindNever]
    public string UserId { get; set; }

    [BindNever]
    public string UserName { get; set; }

    [BindNever]
    public DateTime? CreatedUtc { get; set; }

    [BindNever]
    public DateTime? LastActivityUtc { get; set; }

    [BindNever]
    public List<SelectListItem> Kinds { get; set; } = [];

    [BindNever]
    public List<SelectListItem> Statuses { get; set; } = [];

    public CustomerKind Kind { get; set; }

    public CustomerStatus Status { get; set; }

    public string DisplayName { get; set; }

    public string FirstName { get; set; }

    public string LastName { get; set; }

    public string CompanyName { get; set; }

    public string TaxId { get; set; }

    public bool IsTaxExempt { get; set; }

    public string TaxExemptionReason { get; set; }

    public string Email { get; set; }

    public string Phone { get; set; }

    public bool AcceptsMarketing { get; set; }

    [BindNever]
    public IReadOnlyList<CustomerAddressSummary> Addresses { get; set; } = [];

    /// <summary>
    /// Gets or sets the customer's recent orders, when Orders is enabled.
    /// </summary>
    [BindNever]
    public IReadOnlyList<CustomerOrderSummary> Orders { get; set; } = [];

    [BindNever]
    public bool HasOrders { get; set; }

    /// <summary>
    /// Gets or sets the customer this merged guest moved to.
    /// </summary>
    [BindNever]
    public CustomerMergeSummary MergedInto { get; set; }

    [BindNever]
    public DateTime? MergedUtc { get; set; }

    /// <summary>
    /// Gets or sets the guest customers merged into this one.
    /// </summary>
    [BindNever]
    public IReadOnlyList<CustomerMergeSummary> MergedCustomers { get; set; } = [];

    [BindNever]
    public IReadOnlyList<CustomerPrivacyEntry> PrivacyLog { get; set; } = [];
}

/// <summary>
/// A customer's address book, as their page lists it.
/// </summary>
public class CustomerAddressListViewModel
{
    public IReadOnlyList<CustomerAddressSummary> Addresses { get; set; } = [];

    /// <summary>
    /// The customer, for an administrator's links; empty on the customer's own page.
    /// </summary>
    public string CustomerId { get; set; }

    public bool IsOwn { get; set; }

    public bool CanManage { get; set; }
}

/// <summary>
/// An address as a list shows it.
/// </summary>
public class CustomerAddressSummary
{
    public string AddressId { get; set; }

    public string Label { get; set; }

    public IReadOnlyList<string> Lines { get; set; } = [];

    public bool IsDefaultBilling { get; set; }

    public bool IsDefaultShipping { get; set; }
}

public class CustomerAddressViewModel
{
    [BindNever]
    public string CustomerId { get; set; }

    [BindNever]
    public string CustomerName { get; set; }

    [BindNever]
    public string AddressId { get; set; }

    [BindNever]
    public bool IsNew { get; set; }

    /// <summary>
    /// Whether the customer is editing their own address (My Account) rather than an administrator.
    /// </summary>
    [BindNever]
    public bool IsOwn { get; set; }

    /// <summary>
    /// Gets or sets the address editor, laid out for the address's country.
    /// </summary>
    [BindNever]
    public IShape AddressEditor { get; set; }

    public string Label { get; set; }

    /// <summary>
    /// Gets or sets the address as the editor posts it, under the <c>Address</c> prefix.
    /// </summary>
    public AddressEditorViewModel Address { get; set; } = new();

    public bool IsDefaultBilling { get; set; }

    public bool IsDefaultShipping { get; set; }
}

public class MyAccountViewModel
{
    [BindNever]
    public string Email { get; set; }

    [BindNever]
    public List<SelectListItem> Kinds { get; set; } = [];

    [BindNever]
    public IReadOnlyList<CustomerAddressSummary> Addresses { get; set; } = [];

    public CustomerKind Kind { get; set; }

    public string DisplayName { get; set; }

    public string FirstName { get; set; }

    public string LastName { get; set; }

    public string CompanyName { get; set; }

    public string TaxId { get; set; }

    public string Phone { get; set; }

    public bool AcceptsMarketing { get; set; }
}

public class CustomerOrderSummary
{
    public string OrderId { get; set; }

    public string OrderNumber { get; set; }

    public DateTime CreatedUtc { get; set; }

    public string Status { get; set; }

    public string Total { get; set; }
}

public class CustomerMergeSummary
{
    public string CustomerId { get; set; }

    public string Name { get; set; }
}
