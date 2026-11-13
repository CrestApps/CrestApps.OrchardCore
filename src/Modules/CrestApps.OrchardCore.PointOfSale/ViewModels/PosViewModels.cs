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

    public List<RegisterKeyViewModel> Keys { get; set; } = [];

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
    public IReadOnlyList<PosDepartment> Departments { get; set; } = [];

    [BindNever]
    public bool IsNew
        => string.IsNullOrEmpty(ItemId);
}

public class PosDepartmentsIndexViewModel
{
    [BindNever]
    public AdminListContext List { get; set; }
}

public class PosDepartmentEditViewModel
{
    [BindNever]
    public string ItemId { get; set; }

    public string Name { get; set; }

    public string Number { get; set; }

    public string TaxCategoryCode { get; set; }

    public string Color { get; set; }

    /// <summary>
    /// Gets or sets the least amount, as the number field sends it, in the invariant culture.
    /// </summary>
    public string MinimumAmount { get; set; }

    /// <summary>
    /// Gets or sets the most amount, as the number field sends it, in the invariant culture.
    /// </summary>
    public string MaximumAmount { get; set; }

    public int Position { get; set; }

    public bool IsEnabled { get; set; } = true;

    [BindNever]
    public bool HasTaxCategories { get; set; }

    [BindNever]
    public List<SelectListItem> TaxCategories { get; set; } = [];

    [BindNever]
    public bool IsNew
        => string.IsNullOrEmpty(ItemId);
}

/// <summary>
/// A department key on a register's edit page.
/// </summary>
public class RegisterKeyViewModel
{
    public string DepartmentId { get; set; }

    public string Label { get; set; }

    /// <summary>
    /// Gets or sets the preset amount, as the number field sends it, in the invariant culture.
    /// </summary>
    public string PresetAmount { get; set; }
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
