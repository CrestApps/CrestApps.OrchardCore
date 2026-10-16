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
}
