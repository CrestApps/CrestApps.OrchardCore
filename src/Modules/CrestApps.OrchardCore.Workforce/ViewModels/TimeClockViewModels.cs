using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Workforce.ViewModels;

/// <summary>
/// The kiosk page.
/// </summary>
public sealed class KioskViewModel
{
    public TimeClockKiosk Kiosk { get; set; }

    public string Token { get; set; }

    public string Error { get; set; }

    public StaffMember Worker { get; set; }

    public TimeClockStatus Status { get; set; }

    public string Ticket { get; set; }

    public TimePunch Done { get; set; }
}

/// <summary>
/// The time clock policy editor.
/// </summary>
public class TimeClockPolicyEditViewModel
{
    public string Id { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }

    public string PayPeriodCalendarId { get; set; }

    public int RoundingMinutes { get; set; }

    public string RoundingDirection { get; set; }

    public int EarlyGraceMinutes { get; set; }

    public int LateGraceMinutes { get; set; }

    public int? BlockEarlyPunchInMinutes { get; set; }

    public decimal? AutoMealAfterHours { get; set; }

    public int AutoMealMinutes { get; set; }

    public int? LongMealMinutes { get; set; }

    public IList<string> AllowedSources { get; set; } = [];

    public IList<TimePunchSourceDescriptor> AdditionalSources { get; set; } = [];

    public bool RequireGeofence { get; set; }

    public int GeofenceRadiusMeters { get; set; }

    public string MissedPunchHandling { get; set; }

    public decimal? DailyOvertimeHours { get; set; }

    public decimal? DailyDoubleTimeHours { get; set; }

    public decimal? WeeklyOvertimeHours { get; set; }

    public bool SeventhDayOvertime { get; set; }

    public string Holidays { get; set; }

    public IList<string> WeekendDays { get; set; } = [];

    public IDictionary<string, decimal?> Points { get; set; } = new Dictionary<string, decimal?>();

    public int PointsWindowDays { get; set; }

    public decimal? PointsThreshold { get; set; }

    public bool Enabled { get; set; }

    [BindNever]
    public IList<SelectListItem> CalendarOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the editor, whose fields are grouped in cards by the placement of its shapes.
    /// </summary>
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public global::OrchardCore.DisplayManagement.IShape Editor { get; set; }
}

/// <summary>
/// The Workforce card on the pay code editor: a shift differential's hours.
/// </summary>
public class WorkforcePayCodeWindowViewModel
{
    public TimeOnly? WindowStart { get; set; }

    public TimeOnly? WindowEnd { get; set; }

    public IList<string> Weekdays { get; set; } = [];
}

/// <summary>
/// The kiosk editor.
/// </summary>
public class TimeClockKioskEditViewModel
{
    public string Id { get; set; }

    public string Name { get; set; }

    public string TeamId { get; set; }

    public bool Enabled { get; set; }

    [BindNever]
    public string NewAddress { get; set; }

    [BindNever]
    public IList<SelectListItem> TeamOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the editor, whose fields are grouped in cards by the placement of its shapes.
    /// </summary>
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public global::OrchardCore.DisplayManagement.IShape Editor { get; set; }
}

/// <summary>
/// The timesheet list.
/// </summary>
public sealed class TimesheetListViewModel : WorkforceListViewModel
{
    public string CalendarId { get; set; }

    public DateOnly? PeriodStart { get; set; }

    [BindNever]
    public IList<SelectListItem> CalendarOptions { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> PeriodOptions { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> BulkActions { get; set; } = [];

    [BindNever]
    public int ApprovedInPeriod { get; set; }

    [BindNever]
    public bool PeriodEnded { get; set; }
}

/// <summary>
/// A row of the timesheet list.
/// </summary>
public sealed class TimesheetRowViewModel
{
    public Timesheet Timesheet { get; set; }

    public StaffMember Worker { get; set; }

    public string TeamName { get; set; }

    public IReadOnlyDictionary<string, PayCode> PayCodes { get; set; }
}

/// <summary>
/// One day of a timesheet.
/// </summary>
public sealed class TimesheetDayViewModel
{
    public DateOnly Date { get; set; }

    public IList<TimesheetLine> Lines { get; set; } = [];

    public IList<TimePunch> Punches { get; set; } = [];

    public IList<AttendanceException> Exceptions { get; set; } = [];

    public decimal Hours => Lines.Where(line => line.Source != TimesheetLineSource.Punches || line.PayCodeId is not null).Sum(line => line.Hours);
}

/// <summary>
/// A timesheet in full: its days, lines, punches and exceptions, for the admin and the My Work portal.
/// </summary>
public sealed class TimesheetDetailsViewModel
{
    public Timesheet Timesheet { get; set; }

    public StaffMember Worker { get; set; }

    public string TeamName { get; set; }

    public string TimeZoneId { get; set; }

    public IList<TimesheetDayViewModel> Days { get; set; } = [];

    public IList<(PayCode Code, decimal Hours)> Totals { get; set; } = [];

    public IReadOnlyDictionary<string, PayCode> PayCodes { get; set; }

    public IReadOnlyDictionary<string, string> ActivityNames { get; set; }

    public IList<PunchCorrection> Corrections { get; set; } = [];

    public DateTime? OnTheClockSinceUtc { get; set; }

    public bool CanApprove { get; set; }

    public bool CanEditPunches { get; set; }

    public DateOnly? PreviousPeriod { get; set; }

    public DateOnly? NextPeriod { get; set; }

    public string ReturnUrl { get; set; }
}

/// <summary>
/// The attendance exception list.
/// </summary>
public sealed class AttendanceListViewModel : WorkforceListViewModel
{
    public string WorkerId { get; set; }

    public string Kind { get; set; }

    [BindNever]
    public IList<SelectListItem> KindOptions { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> BulkActions { get; set; } = [];
}

/// <summary>
/// A row of the attendance exception list.
/// </summary>
public sealed class AttendanceRowViewModel
{
    public AttendanceException Exception { get; set; }

    public StaffMember Worker { get; set; }

    public string TeamName { get; set; }
}

/// <summary>
/// The files the earlier payroll export wrote.
/// </summary>
public sealed class PayrollExportViewModel
{
    [BindNever]
    public AdminListContext List { get; set; }
}

/// <summary>
/// A worker's time clock page: their kiosk PIN, attendance points and, read only, their pay rates.
/// </summary>
public sealed class WorkerTimeClockViewModel
{
    [BindNever]
    public StaffMember Worker { get; set; }

    [BindNever]
    public bool HasPin { get; set; }

    [BindNever]
    public DateTime? LockedUntilUtc { get; set; }

    [BindNever]
    public IList<StaffPayRate> Rates { get; set; } = [];

    [BindNever]
    public bool CanViewRates { get; set; }

    [BindNever]
    public decimal Points { get; set; }

    [BindNever]
    public TimeClockPolicy Policy { get; set; }

    [BindNever]
    public TimeClockStatus Status { get; set; }

    [BindNever]
    public IReadOnlyDictionary<string, string> ActivityNames { get; set; }

    public string Pin { get; set; }
}
