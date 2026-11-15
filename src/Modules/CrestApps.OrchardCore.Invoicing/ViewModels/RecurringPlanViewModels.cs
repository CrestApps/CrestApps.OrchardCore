using CrestApps.OrchardCore.Invoicing.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Invoicing.ViewModels;

/// <summary>
/// The editor of a recurring plan: the invoice editor's header and lines, with the schedule.
/// </summary>
public class RecurringPlanEditViewModel : InvoiceEditViewModel
{
    public string Name { get; set; }

    public string Frequency { get; set; } = "Monthly";

    public int Interval { get; set; } = 1;

    public string CustomRule { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public int? MaxRuns { get; set; }

    public RecurringPlanMode Mode { get; set; }

    public decimal? PriceIncreasePercent { get; set; }

    public int? PriceIncreaseEveryRuns { get; set; }

    public bool BillUnbilledCharges { get; set; }

    public List<RecurringUsageChargeEditViewModel> UsageCharges { get; set; } = [];

    /// <summary>
    /// Gets or sets the meters a usage charge can bill, or <see langword="null"/> when Invoicing Billing is off.
    /// </summary>
    public List<SelectListItem> Meters { get; set; }
}

/// <summary>
/// A usage charge in the plan's editor, with a few tier rows (empty ones are left out).
/// </summary>
public class RecurringUsageChargeEditViewModel
{
    public string MeterId { get; set; }

    public string Name { get; set; }

    public UsageTierMode TierMode { get; set; }

    public string TaxCategoryCode { get; set; }

    public List<UsageTierEditViewModel> Tiers { get; set; } = [];
}

/// <summary>
/// A price tier row of a usage charge.
/// </summary>
public class UsageTierEditViewModel
{
    public decimal? UpTo { get; set; }

    public decimal? UnitPrice { get; set; }
}

public class RecurringPlansAdminIndexOptions
{
    public string Search { get; set; }

    public RecurringPlanStatus? Status { get; set; }

    public List<SelectListItem> Statuses { get; set; }
}

public class RecurringPlansAdminIndexViewModel
{
    public RecurringPlansAdminIndexOptions Options { get; set; } = new();

    public AdminListContext List { get; set; }

    public bool CanCreate { get; set; }
}

public class RecurringPlanRowViewModel
{
    public RecurringPlan Plan { get; set; }

    public bool CanEdit { get; set; }
}

public class RecurringPlanDetailViewModel
{
    public RecurringPlan Plan { get; set; }

    public IReadOnlyList<DateOnly> Upcoming { get; set; } = [];

    public bool CanManage { get; set; }

    /// <summary>
    /// Gets or sets the names and units of the meters the plan's usage charges bill, by id.
    /// </summary>
    public IReadOnlyDictionary<string, UsageMeter> Meters { get; set; } = new Dictionary<string, UsageMeter>();
}
