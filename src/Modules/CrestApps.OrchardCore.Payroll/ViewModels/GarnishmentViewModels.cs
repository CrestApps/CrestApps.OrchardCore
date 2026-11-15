using CrestApps.OrchardCore.Addresses.Core.Editing;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Payroll.ViewModels;

/// <summary>
/// The garnishment order editor.
/// </summary>
public class GarnishmentOrderViewModel
{
    public string ProfileId { get; set; }

    public string Type { get; set; }

    public string CaseNumber { get; set; }

    public string Agency { get; set; }

    public string PayCodeId { get; set; }

    public string Amount { get; set; }

    public string Percent { get; set; }

    public string LimitPercent { get; set; }

    public string ProtectedWeeklyAmount { get; set; }

    public string ExemptAmount { get; set; }

    public bool OtherDependents { get; set; }

    public bool ArrearsOver12Weeks { get; set; }

    [BindNever]
    public string OrderId { get; set; }

    public string TotalOwed { get; set; }

    public string Priority { get; set; }

    public DateOnly? ReceivedDate { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public string Status { get; set; }

    public string PayeeName { get; set; }

    public string PaymentMethod { get; set; }

    public string PayeeBankAccountId { get; set; }

    public string Notes { get; set; }

    [BindNever]
    public bool IsNew { get; set; }

    [BindNever]
    public string ProfileName { get; set; }

    [BindNever]
    public decimal Withheld { get; set; }

    [BindNever]
    public decimal? Remaining { get; set; }

    [BindNever]
    public string Currency { get; set; }

    [BindNever]
    public IList<SelectListItem> Profiles { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> PayCodes { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> BankAccounts { get; set; } = [];

    [BindNever]
    public IList<CrestApps.OrchardCore.Payroll.Models.GarnishmentRemittance> Remittances { get; set; } = [];
}

/// <summary>
/// The payee's address card of the garnishment order editor.
/// </summary>
public class GarnishmentPayeeAddressViewModel
{
    public AddressEditorViewModel Address { get; set; } = new();

    [BindNever]
    public IShape AddressEditor { get; set; }
}

/// <summary>
/// A garnishment row.
/// </summary>
public class GarnishmentRowViewModel
{
    public CrestApps.OrchardCore.Payroll.Models.GarnishmentOrder Order { get; set; }

    public string StaffName { get; set; }

    public string EmployerName { get; set; }
}
