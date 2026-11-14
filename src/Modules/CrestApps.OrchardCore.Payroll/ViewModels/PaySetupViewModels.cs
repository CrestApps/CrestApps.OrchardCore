using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Payroll.ViewModels;

/// <summary>
/// The editor of a pay schedule.
/// </summary>
public class PayScheduleViewModel
{
    public string Name { get; set; }

    public string Frequency { get; set; }

    public DateOnly? AnchorDate { get; set; }

    public string WeekStart { get; set; }

    public int SecondPeriodStartDay { get; set; } = 16;

    public string PayDateRule { get; set; }

    public int PayDateOffsetDays { get; set; } = 5;

    public int FirstPayDay { get; set; } = 15;

    public int SecondPayDay { get; set; }

    public string NonBusinessDayShift { get; set; }

    public int ApprovalLeadDays { get; set; } = 2;

    public bool Enabled { get; set; } = true;

    [BindNever]
    public IList<string> NextPeriods { get; set; } = [];
}

/// <summary>
/// The editor of a pay code.
/// </summary>
public class PayCodeViewModel
{
    public string Code { get; set; }

    public string Name { get; set; }

    public string Class { get; set; }

    public string Kind { get; set; }

    public string Multiplier { get; set; }

    public string PremiumPerHour { get; set; }

    public string ExportCode { get; set; }

    public bool Enabled { get; set; } = true;

    [BindNever]
    public IList<SelectListItem> KindOptions { get; set; } = [];
}
