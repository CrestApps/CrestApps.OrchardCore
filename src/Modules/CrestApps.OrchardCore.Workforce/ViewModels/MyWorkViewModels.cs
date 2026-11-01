using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Workforce.ViewModels;

/// <summary>
/// The portal's navigation.
/// </summary>
public class MyWorkNavViewModel
{
    /// <summary>
    /// Gets or sets the page shown.
    /// </summary>
    public string Active { get; set; }

    /// <summary>
    /// Gets or sets the name shown.
    /// </summary>
    public string WorkerName { get; set; }

    /// <summary>
    /// Gets or sets whether the user is a worker.
    /// </summary>
    public bool IsWorker { get; set; }

    /// <summary>
    /// Gets or sets how many notifications are unread.
    /// </summary>
    public int Unread { get; set; }

    /// <summary>
    /// Gets or sets whether shift planning is on.
    /// </summary>
    public bool HasShiftPlanning { get; set; }

    /// <summary>
    /// Gets or sets whether the worker can request time off here.
    /// </summary>
    public bool HasTimeOff { get; set; }

    /// <summary>
    /// Gets or sets whether the user manages teams.
    /// </summary>
    public bool IsManager { get; set; }

    /// <summary>
    /// Gets or sets whether the worker clocks in and out, so the timesheet is shown.
    /// </summary>
    public bool HasTimeClock { get; set; }
}

/// <summary>
/// A shift or time off on a worker's own pages.
/// </summary>
public class MyShiftViewModel
{
    /// <summary>
    /// Gets or sets the calendar event.
    /// </summary>
    public string EventId { get; set; }

    /// <summary>
    /// Gets or sets the day it starts.
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// Gets or sets when it starts, on the team's clock.
    /// </summary>
    public DateTime Start { get; set; }

    /// <summary>
    /// Gets or sets when it ends, on the team's clock.
    /// </summary>
    public DateTime End { get; set; }

    /// <summary>
    /// Gets or sets when it starts, in UTC.
    /// </summary>
    public DateTime StartUtc { get; set; }

    /// <summary>
    /// Gets or sets the team.
    /// </summary>
    public string TeamName { get; set; }

    /// <summary>
    /// Gets or sets the breaks and activities inside it, written out.
    /// </summary>
    public string Segments { get; set; }

    /// <summary>
    /// Gets or sets whether it is time off rather than a shift.
    /// </summary>
    public bool IsTimeOff { get; set; }

    /// <summary>
    /// Gets or sets the title of time off.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets whether it lasts all day.
    /// </summary>
    public bool AllDay { get; set; }
}

/// <summary>
/// The portal's home page.
/// </summary>
public class MyWorkHomeViewModel
{
    /// <summary>
    /// Gets or sets the navigation.
    /// </summary>
    public MyWorkNavViewModel Nav { get; set; }

    /// <summary>
    /// Gets or sets the next shift.
    /// </summary>
    public MyShiftViewModel NextShift { get; set; }

    /// <summary>
    /// Gets or sets today's shifts and time off.
    /// </summary>
    public IList<MyShiftViewModel> Today { get; set; } = [];

    /// <summary>
    /// Gets or sets the latest notifications.
    /// </summary>
    public IList<WorkforceInboxItem> Inbox { get; set; } = [];

    /// <summary>
    /// Gets or sets where the worker stands on the clock; <see langword="null"/> when they don't clock in.
    /// </summary>
    public TimeClockStatus Clock { get; set; }
}

/// <summary>
/// A worker's schedule.
/// </summary>
public class MyScheduleViewModel
{
    /// <summary>
    /// Gets or sets the navigation.
    /// </summary>
    public MyWorkNavViewModel Nav { get; set; }

    /// <summary>
    /// Gets or sets the shifts and time off, by day.
    /// </summary>
    public IList<(DateOnly Date, IList<MyShiftViewModel> Items)> Days { get; set; } = [];
}

/// <summary>
/// A worker's time off.
/// </summary>
public class MyTimeOffViewModel
{
    /// <summary>
    /// Gets or sets the navigation.
    /// </summary>
    [BindNever]
    public MyWorkNavViewModel Nav { get; set; }

    /// <summary>
    /// Gets or sets the balances.
    /// </summary>
    [BindNever]
    public IList<TimeOffBalance> Balances { get; set; } = [];

    /// <summary>
    /// Gets or sets the requests.
    /// </summary>
    [BindNever]
    public IList<(TimeOffRequest Request, TimeOffType Type)> Requests { get; set; } = [];

    /// <summary>
    /// Gets or sets the types the worker can request.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> TypeOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets what a checked request would do.
    /// </summary>
    [BindNever]
    public TimeOffPreview Preview { get; set; }

    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    public string TypeId { get; set; }

    /// <summary>
    /// Gets or sets the first day.
    /// </summary>
    public DateOnly? StartDate { get; set; }

    /// <summary>
    /// Gets or sets the last day.
    /// </summary>
    public DateOnly? EndDate { get; set; }

    /// <summary>
    /// Gets or sets whether only part of one day is asked for.
    /// </summary>
    public bool PartialDay { get; set; }

    /// <summary>
    /// Gets or sets when part of a day starts.
    /// </summary>
    public string StartTime { get; set; }

    /// <summary>
    /// Gets or sets when part of a day ends.
    /// </summary>
    public string EndTime { get; set; }

    /// <summary>
    /// Gets or sets the reason.
    /// </summary>
    public string Reason { get; set; }
}

/// <summary>
/// An open shift on the worker's shifts page.
/// </summary>
public class MyOpenShiftViewModel
{
    /// <summary>
    /// Gets or sets the open shift.
    /// </summary>
    public OpenShift Shift { get; set; }

    /// <summary>
    /// Gets or sets the team.
    /// </summary>
    public string TeamName { get; set; }

    /// <summary>
    /// Gets or sets why the worker can't take it.
    /// </summary>
    public IReadOnlyList<string> Problems { get; set; } = [];

    /// <summary>
    /// Gets or sets the worker's claim on it.
    /// </summary>
    public OpenShiftClaim MyClaim { get; set; }
}

/// <summary>
/// A shift trade on the worker's shifts page.
/// </summary>
public class MyTradeViewModel
{
    /// <summary>
    /// Gets or sets the trade.
    /// </summary>
    public ShiftTrade Trade { get; set; }

    /// <summary>
    /// Gets or sets who offered it.
    /// </summary>
    public string FromName { get; set; }

    /// <summary>
    /// Gets or sets who it is offered to.
    /// </summary>
    public string ToName { get; set; }

    /// <summary>
    /// Gets or sets the shift offered, written out.
    /// </summary>
    public string ShiftText { get; set; }

    /// <summary>
    /// Gets or sets the shift wanted in exchange, written out.
    /// </summary>
    public string TargetShiftText { get; set; }
}

/// <summary>
/// A worker's shifts to claim and trade.
/// </summary>
public class MyShiftsViewModel
{
    /// <summary>
    /// Gets or sets the navigation.
    /// </summary>
    public MyWorkNavViewModel Nav { get; set; }

    /// <summary>
    /// Gets or sets the open shifts of the worker's teams.
    /// </summary>
    public IList<MyOpenShiftViewModel> OpenShifts { get; set; } = [];

    /// <summary>
    /// Gets or sets the worker's upcoming shifts.
    /// </summary>
    public IList<MyShiftViewModel> MyShifts { get; set; } = [];

    /// <summary>
    /// Gets or sets the trades offered to the worker.
    /// </summary>
    public IList<MyTradeViewModel> OfferedToMe { get; set; } = [];

    /// <summary>
    /// Gets or sets the trades the worker offered.
    /// </summary>
    public IList<MyTradeViewModel> MyOffers { get; set; } = [];

    /// <summary>
    /// Gets or sets the worker's colleagues.
    /// </summary>
    public IList<SelectListItem> Colleagues { get; set; } = [];

    /// <summary>
    /// Gets or sets the colleagues' upcoming shifts, to swap for.
    /// </summary>
    public IList<SelectListItem> ColleagueShifts { get; set; } = [];
}

/// <summary>
/// A worker's availability.
/// </summary>
public class MyAvailabilityViewModel
{
    /// <summary>
    /// Gets or sets the navigation.
    /// </summary>
    [BindNever]
    public MyWorkNavViewModel Nav { get; set; }

    /// <summary>
    /// Gets or sets the availability, one line per day.
    /// </summary>
    public string Availability { get; set; }
}

/// <summary>
/// A channel or a kind of notification the worker can turn on or off.
/// </summary>
public class MyNotificationOption
{
    /// <summary>
    /// Gets or sets the value posted.
    /// </summary>
    public string Value { get; set; }

    /// <summary>
    /// Gets or sets what it is called.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets whether it is on.
    /// </summary>
    public bool Enabled { get; set; }
}

/// <summary>
/// A worker's portal settings.
/// </summary>
public class MySettingsViewModel
{
    /// <summary>
    /// Gets or sets the navigation.
    /// </summary>
    public MyWorkNavViewModel Nav { get; set; }

    /// <summary>
    /// Gets or sets the channels.
    /// </summary>
    public IList<MyNotificationOption> Channels { get; set; } = [];

    /// <summary>
    /// Gets or sets the kinds of notification.
    /// </summary>
    public IList<MyNotificationOption> Kinds { get; set; } = [];

    /// <summary>
    /// Gets or sets the devices push is on for.
    /// </summary>
    public IList<WebPushSubscription> Devices { get; set; } = [];

    /// <summary>
    /// Gets or sets the address of the worker's calendar feed.
    /// </summary>
    public string FeedUrl { get; set; }

    /// <summary>
    /// Gets or sets the site's public push key.
    /// </summary>
    public string PushPublicKey { get; set; }
}

/// <summary>
/// A manager's approvals.
/// </summary>
public class MyTimesheetViewModel
{
    /// <summary>
    /// Gets or sets the portal menu.
    /// </summary>
    public MyWorkNavViewModel Nav { get; set; }

    /// <summary>
    /// Gets or sets the timesheet.
    /// </summary>
    public TimesheetDetailsViewModel Details { get; set; }

    /// <summary>
    /// Gets or sets why there is no timesheet.
    /// </summary>
    public string Problem { get; set; }

    /// <summary>
    /// Gets or sets what the worker attests to.
    /// </summary>
    public string Attestation { get; set; }
}

/// <summary>
/// The approvals waiting for a manager in the My Work portal.
/// </summary>
public class MyApprovalsViewModel
{
    /// <summary>
    /// Gets or sets the navigation.
    /// </summary>
    public MyWorkNavViewModel Nav { get; set; }

    /// <summary>
    /// Gets or sets the time off waiting.
    /// </summary>
    public IList<TimeOffRequestRowViewModel> TimeOff { get; set; } = [];

    /// <summary>
    /// Gets or sets the trades waiting for approval.
    /// </summary>
    public IList<MyTradeViewModel> Trades { get; set; } = [];

    /// <summary>
    /// Gets or sets the open shifts with claims waiting.
    /// </summary>
    public IList<(OpenShift Shift, string TeamName, IList<(OpenShiftClaim Claim, string WorkerName)> Claims)> Claims { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the user can decide time off.
    /// </summary>
    public bool CanTimeOff { get; set; }

    /// <summary>
    /// Gets or sets whether the user can decide trades.
    /// </summary>
    public bool CanTrades { get; set; }

    /// <summary>
    /// Gets or sets whether the user can decide claims.
    /// </summary>
    public bool CanClaims { get; set; }

    /// <summary>
    /// Gets or sets whether the user approves timesheets.
    /// </summary>
    public bool CanTimesheets { get; set; }

    /// <summary>
    /// Gets or sets the submitted timesheets, with their worker's name.
    /// </summary>
    public IList<(Timesheet Timesheet, string WorkerName)> Timesheets { get; set; } = [];

    /// <summary>
    /// Gets or sets the punches workers asked for, with the worker's name and the punch's local time.
    /// </summary>
    public IList<(PunchCorrection Correction, string WorkerName, string When)> Corrections { get; set; } = [];

    /// <summary>
    /// Gets or sets the open attendance exceptions, with their worker's name.
    /// </summary>
    public IList<(AttendanceException Exception, string WorkerName)> Exceptions { get; set; } = [];
}

/// <summary>
/// Who is working now in the teams a manager manages.
/// </summary>
public class MyTeamNowViewModel
{
    /// <summary>
    /// Gets or sets the navigation.
    /// </summary>
    public MyWorkNavViewModel Nav { get; set; }

    /// <summary>
    /// Gets or sets now, on the site's clock.
    /// </summary>
    public DateTime Now { get; set; }

    /// <summary>
    /// Gets or sets the workers on shift, with their team, their shift and what they are doing now.
    /// </summary>
    public IList<(string Name, string TeamName, DateTime Start, DateTime End, string Doing)> Working { get; set; } = [];

    /// <summary>
    /// Gets or sets the workers off today.
    /// </summary>
    public IList<(string Name, string TeamName, string Title)> Off { get; set; } = [];
}
