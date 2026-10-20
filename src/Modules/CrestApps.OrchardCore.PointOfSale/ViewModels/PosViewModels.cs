using CrestApps.OrchardCore.PointOfSale.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.PointOfSale.ViewModels;

public class RegistersIndexViewModel
{
    [BindNever]
    public AdminListContext List { get; set; }
}

public class RegisterEditViewModel
{
    [BindNever]
    public string ItemId { get; set; }

    public string Name { get; set; }

    public string LocationId { get; set; }

    public string Currency { get; set; }

    public bool IsEnabled { get; set; } = true;

    public List<string> Roles { get; set; } = [];

    public string AddressLine1 { get; set; }

    public string AddressLine2 { get; set; }

    public string City { get; set; }

    public string Region { get; set; }

    public string PostalCode { get; set; }

    public string Country { get; set; }

    [BindNever]
    public List<SelectListItem> Locations { get; set; } = [];

    [BindNever]
    public IReadOnlyList<string> AvailableRoles { get; set; } = [];

    [BindNever]
    public bool IsNew
        => string.IsNullOrEmpty(ItemId);
}

public class RegisterShiftsViewModel
{
    public RegisterShiftsOptions Options { get; set; } = new();

    [BindNever]
    public AdminListContext List { get; set; }
}

public class RegisterShiftsOptions
{
    public string RegisterId { get; set; }

    [BindNever]
    public List<SelectListItem> Registers { get; set; } = [];
}

public class RegisterShiftDetailViewModel
{
    public RegisterShift Shift { get; set; }

    public RegisterShiftSummary Summary { get; set; }

    public decimal ExpectedCash { get; set; }
}
