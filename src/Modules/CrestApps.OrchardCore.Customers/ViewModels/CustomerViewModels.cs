using CrestApps.OrchardCore.Addresses.Core.Editing;
using CrestApps.OrchardCore.Customers.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
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

    public string Email { get; set; }

    public string Phone { get; set; }

    public bool AcceptsMarketing { get; set; }

    [BindNever]
    public IReadOnlyList<CustomerAddressSummary> Addresses { get; set; } = [];
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
