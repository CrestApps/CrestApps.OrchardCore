using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.TimeTracker.Models;

/// <summary>
/// A group that time is totalled by, such as Payable, Billable, Break or Vacation. Each is a column of the time report.
/// </summary>
public sealed class TimeCategory : CatalogItem
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets a short code for exports, such as PAY.
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Gets or sets the color.
    /// </summary>
    public string Color { get; set; }

    /// <summary>
    /// Gets or sets where the column sits in the report; lower first.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// Gets or sets how a minute counts: 1 adds it as it is, 1.5 counts it half again (an overtime premium), 0.5 half.
    /// </summary>
    public decimal Factor { get; set; } = 1;

    /// <summary>
    /// Gets or sets whether it is in use.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets when it was made, in UTC.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when it last changed, in UTC.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}

/// <summary>
/// Something a person does while their time is tracked, such as Work, Break or Lunch.
/// </summary>
public sealed class TimeStage : CatalogItem
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets a short code for exports and integrations, such as WORK.
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Gets or sets the color.
    /// </summary>
    public string Color { get; set; }

    /// <summary>
    /// Gets or sets where it sits in lists and on the clock; lower first.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// Gets or sets the categories its time counts toward.
    /// </summary>
    public List<string> CategoryIds { get; set; } = [];

    /// <summary>
    /// Gets or sets whether people pick it themselves on their clock; others come only from integrations or managers.
    /// </summary>
    public bool SelfService { get; set; } = true;

    /// <summary>
    /// Gets or sets whether it is in use.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets when it was made, in UTC.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when it last changed, in UTC.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}

/// <summary>
/// A change made to a time entry after it was recorded, kept with it.
/// </summary>
public sealed class TimeEntryChange
{
    /// <summary>
    /// Gets or sets when it was changed, in UTC.
    /// </summary>
    public DateTime Utc { get; set; }

    /// <summary>
    /// Gets or sets who changed it.
    /// </summary>
    public string By { get; set; }

    /// <summary>
    /// Gets or sets why.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets what it was before, such as "Work 09:00-12:00".
    /// </summary>
    public string Before { get; set; }
}

/// <summary>
/// A span of a person's time in one stage. An open entry has no end yet: the person is in that stage now.
/// </summary>
public sealed class TimeEntry : CatalogItem
{
    /// <summary>
    /// Gets or sets the person.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the person's user name, kept for lists.
    /// </summary>
    public string UserName { get; set; }

    /// <summary>
    /// Gets or sets the stage.
    /// </summary>
    public string StageId { get; set; }

    /// <summary>
    /// Gets or sets when it starts, in UTC.
    /// </summary>
    public DateTime StartUtc { get; set; }

    /// <summary>
    /// Gets or sets when it ends, in UTC, once it has.
    /// </summary>
    public DateTime? EndUtc { get; set; }

    /// <summary>
    /// Gets or sets where it came from, one of <see cref="TimeTrackerConstants.Sources"/>.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets what in the source it stands for, such as a time-off request.
    /// </summary>
    public string SourceRef { get; set; }

    /// <summary>
    /// Gets or sets a note.
    /// </summary>
    public string Note { get; set; }

    /// <summary>
    /// Gets or sets who recorded it.
    /// </summary>
    public string CreatedBy { get; set; }

    /// <summary>
    /// Gets or sets when it was recorded, in UTC.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the changes made to it since, oldest first.
    /// </summary>
    public List<TimeEntryChange> Changes { get; set; } = [];

    /// <summary>
    /// Gets whether the person is still in it.
    /// </summary>
    public bool IsOpen => EndUtc is null;
}

/// <summary>
/// A person's clock now.
/// </summary>
public sealed class TimeTrackerStatus
{
    /// <summary>
    /// Gets or sets the person.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the entry they are in, if they are clocked in.
    /// </summary>
    public TimeEntry Open { get; set; }

    /// <summary>
    /// Gets whether they are clocked in.
    /// </summary>
    public bool IsClockedIn => Open is not null;
}

/// <summary>
/// Totals of one person, for a day or for the whole range.
/// </summary>
public sealed class TimeTotalsRow
{
    /// <summary>
    /// Gets or sets the person.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the person's user name.
    /// </summary>
    public string UserName { get; set; }

    /// <summary>
    /// Gets or sets the day, when totals are by day.
    /// </summary>
    public DateOnly? Date { get; set; }

    /// <summary>
    /// Gets or sets the minutes in each category, after its factor, by category.
    /// </summary>
    public Dictionary<string, decimal> MinutesByCategory { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the minutes in each stage, by stage.
    /// </summary>
    public Dictionary<string, int> MinutesByStage { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets every tracked minute.
    /// </summary>
    public int TotalMinutes { get; set; }
}

/// <summary>
/// What time entries to list.
/// </summary>
public sealed class TimeEntryQuery
{
    /// <summary>
    /// Gets or sets the people, or <see langword="null"/> for everyone.
    /// </summary>
    public IReadOnlyCollection<string> UserIds { get; set; }

    /// <summary>
    /// Gets or sets the stage, if one.
    /// </summary>
    public string StageId { get; set; }

    /// <summary>
    /// Gets or sets the start of the range, in UTC.
    /// </summary>
    public DateTime? FromUtc { get; set; }

    /// <summary>
    /// Gets or sets the end of the range, in UTC.
    /// </summary>
    public DateTime? ToUtc { get; set; }
}

/// <summary>
/// The outcome of a change to someone's time.
/// </summary>
public sealed class TimeTrackerResult
{
    /// <summary>
    /// Gets or sets the entry as it is now.
    /// </summary>
    public TimeEntry Entry { get; set; }

    /// <summary>
    /// Gets or sets what stopped the change.
    /// </summary>
    public IReadOnlyList<string> Errors { get; set; } = [];

    /// <summary>
    /// Gets whether the change was made.
    /// </summary>
    public bool Succeeded => Errors.Count == 0;

    /// <summary>
    /// Makes a failed result.
    /// </summary>
    /// <param name="error">What stopped the change.</param>
    /// <returns>The result.</returns>
    public static TimeTrackerResult Fail(string error) => new() { Errors = [error] };
}

/// <summary>
/// What happened to a time entry, for observers.
/// </summary>
public enum TimeEntryChangeKind
{
    /// <summary>
    /// It was recorded.
    /// </summary>
    Created,

    /// <summary>
    /// It ended, when the person switched stage or clocked out.
    /// </summary>
    Ended,

    /// <summary>
    /// Someone changed it.
    /// </summary>
    Edited,

    /// <summary>
    /// Someone removed it.
    /// </summary>
    Deleted,
}
