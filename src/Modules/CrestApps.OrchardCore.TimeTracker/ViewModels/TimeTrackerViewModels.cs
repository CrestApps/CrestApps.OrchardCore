using CrestApps.OrchardCore.TimeTracker.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.TimeTracker.ViewModels;

/// <summary>
/// The person's own clock and day.
/// </summary>
public class MyTimeViewModel
{
    /// <summary>
    /// Gets or sets the clock now.
    /// </summary>
    public TimeTrackerStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the stages the person may pick.
    /// </summary>
    public IList<TimeStage> Stages { get; set; } = [];

    /// <summary>
    /// Gets or sets the clock that keeps this person's time instead, if another one does.
    /// </summary>
    public ExternalTimeClockInfo ExternalClock { get; set; }

    /// <summary>
    /// Gets or sets every stage, by id.
    /// </summary>
    public IDictionary<string, TimeStage> AllStages { get; set; } = new Dictionary<string, TimeStage>(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets today's entries.
    /// </summary>
    public IList<TimeEntry> Today { get; set; } = [];

    /// <summary>
    /// Gets or sets the week's totals, by day.
    /// </summary>
    public IList<TimeTotalsRow> Week { get; set; } = [];

    /// <summary>
    /// Gets or sets the categories, in report order.
    /// </summary>
    public IList<TimeCategory> Categories { get; set; } = [];

    /// <summary>
    /// Gets or sets the time zone.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets the start of today, in UTC.
    /// </summary>
    public DateTime DayStartUtc { get; set; }

    /// <summary>
    /// Gets or sets now, in UTC.
    /// </summary>
    public DateTime NowUtc { get; set; }
}

/// <summary>
/// A time entry in the entries list.
/// </summary>
public sealed class TimeEntryRow
{
    /// <summary>
    /// Gets or sets the entry.
    /// </summary>
    public TimeEntry Entry { get; set; }

    /// <summary>
    /// Gets or sets its stage.
    /// </summary>
    public TimeStage Stage { get; set; }

    /// <summary>
    /// Gets or sets the time zone.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets whether the user can change it.
    /// </summary>
    public bool CanManage { get; set; }

    /// <summary>
    /// Gets or sets the name of the clock that keeps the entry, if another one does.
    /// </summary>
    public string KeptBy { get; set; }
}

/// <summary>
/// The entries list and its filters.
/// </summary>
public class TimeEntriesListViewModel
{
    /// <summary>
    /// Gets or sets the search text, a user name.
    /// </summary>
    public string Q { get; set; }

    /// <summary>
    /// Gets or sets the stage filter.
    /// </summary>
    public string StageId { get; set; }

    /// <summary>
    /// Gets or sets the day filter, as yyyy-MM-dd.
    /// </summary>
    public string Day { get; set; }

    /// <summary>
    /// Gets or sets the stages to filter by.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> StageOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the user can add time.
    /// </summary>
    [BindNever]
    public bool CanManage { get; set; }

    /// <summary>
    /// Gets or sets the rows.
    /// </summary>
    [BindNever]
    public AdminListContext<TimeEntryRow> List { get; set; }
}

/// <summary>
/// The form that adds or changes a time entry.
/// </summary>
public class TimeEntryEditViewModel
{
    /// <summary>
    /// Gets or sets the entry, when it is changed.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the person, when it is added.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the person's name, when it is changed.
    /// </summary>
    [BindNever]
    public string UserName { get; set; }

    /// <summary>
    /// Gets or sets the stage.
    /// </summary>
    public string StageId { get; set; }

    /// <summary>
    /// Gets or sets the start, in the site's time.
    /// </summary>
    public DateTime? Start { get; set; }

    /// <summary>
    /// Gets or sets the end, in the site's time; empty while it is open.
    /// </summary>
    public DateTime? End { get; set; }

    /// <summary>
    /// Gets or sets the note.
    /// </summary>
    public string Note { get; set; }

    /// <summary>
    /// Gets or sets why it is added or changed.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets whether the entry is still open.
    /// </summary>
    [BindNever]
    public bool IsOpen { get; set; }

    /// <summary>
    /// Gets or sets the clock that keeps the entry, if another one does; it is then corrected there.
    /// </summary>
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public ExternalTimeClockInfo ExternalClock { get; set; }

    /// <summary>
    /// Gets or sets the changes made to it so far.
    /// </summary>
    [BindNever]
    public IList<TimeEntryChange> Changes { get; set; } = [];

    /// <summary>
    /// Gets or sets the stages to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> StageOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the time zone.
    /// </summary>
    [BindNever]
    public string TimeZoneId { get; set; }
}

/// <summary>
/// The time report.
/// </summary>
public class TimeReportViewModel
{
    /// <summary>
    /// Gets or sets the first day, as yyyy-MM-dd.
    /// </summary>
    public string From { get; set; }

    /// <summary>
    /// Gets or sets the last day, as yyyy-MM-dd.
    /// </summary>
    public string To { get; set; }

    /// <summary>
    /// Gets or sets whether each day has its own row.
    /// </summary>
    public bool ByDay { get; set; }

    /// <summary>
    /// Gets or sets the user name filter.
    /// </summary>
    public string Q { get; set; }

    /// <summary>
    /// Gets or sets the categories, the report's columns.
    /// </summary>
    [BindNever]
    public IList<TimeCategory> Categories { get; set; } = [];

    /// <summary>
    /// Gets or sets the rows.
    /// </summary>
    [BindNever]
    public IList<TimeTotalsRow> Rows { get; set; } = [];

    /// <summary>
    /// Gets or sets the time zone.
    /// </summary>
    [BindNever]
    public string TimeZoneId { get; set; }
}

/// <summary>
/// A stage or category in its list.
/// </summary>
public sealed class TimeSettingRow
{
    /// <summary>
    /// Gets or sets the kind: stages or categories.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets the record.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the code.
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Gets or sets the color.
    /// </summary>
    public string Color { get; set; }

    /// <summary>
    /// Gets or sets what else to say about it.
    /// </summary>
    public string Detail { get; set; }

    /// <summary>
    /// Gets or sets whether it is in use.
    /// </summary>
    public bool Enabled { get; set; }
}

/// <summary>
/// A list of stages or categories.
/// </summary>
public class TimeSettingsListViewModel
{
    /// <summary>
    /// Gets or sets the kind: stages or categories.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets the rows.
    /// </summary>
    [BindNever]
    public AdminListContext<TimeSettingRow> List { get; set; }
}

/// <summary>
/// The form of a category.
/// </summary>
public class TimeCategoryEditViewModel
{
    /// <summary>
    /// Gets or sets the record, when it is changed.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the code.
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Gets or sets the color.
    /// </summary>
    public string Color { get; set; }

    /// <summary>
    /// Gets or sets the column's place.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// Gets or sets how a minute counts.
    /// </summary>
    public decimal Factor { get; set; } = 1;

    /// <summary>
    /// Gets or sets whether it is in use.
    /// </summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// The form of a stage.
/// </summary>
public class TimeStageEditViewModel
{
    /// <summary>
    /// Gets or sets the record, when it is changed.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the code.
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Gets or sets the color.
    /// </summary>
    public string Color { get; set; }

    /// <summary>
    /// Gets or sets its place.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// Gets or sets the categories its time counts toward.
    /// </summary>
    public List<string> CategoryIds { get; set; } = [];

    /// <summary>
    /// Gets or sets whether people pick it themselves.
    /// </summary>
    public bool SelfService { get; set; } = true;

    /// <summary>
    /// Gets or sets whether it is in use.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the categories to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> CategoryOptions { get; set; } = [];
}
