using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.WorkOrders.Models;

/// <summary>
/// Shows a question only after another was answered a certain way.
/// </summary>
public sealed class ChecklistCondition
{
    /// <summary>
    /// Gets or sets the question it depends on.
    /// </summary>
    public string QuestionId { get; set; }

    /// <summary>
    /// Gets or sets the answer that shows it.
    /// </summary>
    public string EqualsValue { get; set; }
}

/// <summary>
/// A question on a checklist.
/// </summary>
public sealed class ChecklistQuestion
{
    /// <summary>
    /// Gets or sets the question identifier.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the section it is in.
    /// </summary>
    public string Section { get; set; }

    /// <summary>
    /// Gets or sets the question.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets the kind of answer.
    /// </summary>
    public ChecklistQuestionKind Kind { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether it must be answered.
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Gets or sets help text.
    /// </summary>
    public string Help { get; set; }

    /// <summary>
    /// Gets or sets the lowest number accepted.
    /// </summary>
    public decimal? Min { get; set; }

    /// <summary>
    /// Gets or sets the highest number accepted.
    /// </summary>
    public decimal? Max { get; set; }

    /// <summary>
    /// Gets or sets the unit of a number.
    /// </summary>
    public string Unit { get; set; }

    /// <summary>
    /// Gets or sets the choices.
    /// </summary>
    public List<string> Choices { get; set; } = [];

    /// <summary>
    /// Gets or sets how many photos it needs.
    /// </summary>
    public int MinPhotos { get; set; }

    /// <summary>
    /// Gets or sets the asset meter a reading is for.
    /// </summary>
    public string Meter { get; set; }

    /// <summary>
    /// Gets or sets when it shows, or <see langword="null"/> for always.
    /// </summary>
    public ChecklistCondition ShowWhen { get; set; }
}

/// <summary>
/// A checklist to fill in on a job, such as an inspection.
/// </summary>
public sealed class ChecklistTemplate : CatalogItem
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the questions, in order.
    /// </summary>
    public List<ChecklistQuestion> Questions { get; set; } = [];

    /// <summary>
    /// Gets or sets when it was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}

/// <summary>
/// The same opening hours every week, such as weekdays from 8 to 6.
/// </summary>
public sealed class WeeklyHours
{
    // Lists start empty: a stored record is read into a new instance, and default items would be read twice.

    /// <summary>
    /// Gets or sets the days open.
    /// </summary>
    public List<DayOfWeek> Days { get; set; } = [];

    /// <summary>
    /// Gives Monday to Friday, the usual days open.
    /// </summary>
    /// <returns>The days.</returns>
    public static List<DayOfWeek> Weekdays() => [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    /// <summary>
    /// Gets or sets when they open.
    /// </summary>
    public TimeOnly Opens { get; set; } = new(9, 0);

    /// <summary>
    /// Gets or sets when they close.
    /// </summary>
    public TimeOnly Closes { get; set; } = new(17, 0);
}

/// <summary>
/// The targets of one priority.
/// </summary>
public sealed class ServiceLevelTarget
{
    /// <summary>
    /// Gets or sets the priority.
    /// </summary>
    public WorkOrderPriority Priority { get; set; }

    /// <summary>
    /// Gets or sets the minutes to the first response, or 0 for none.
    /// </summary>
    public int ResponseMinutes { get; set; }

    /// <summary>
    /// Gets or sets the minutes to resolution, or 0 for none.
    /// </summary>
    public int ResolveMinutes { get; set; }
}

/// <summary>
/// How fast work orders must be answered and resolved, by priority.
/// </summary>
public sealed class ServiceLevelPolicy : CatalogItem
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the targets, by priority.
    /// </summary>
    public List<ServiceLevelTarget> Targets { get; set; } = [];

    /// <summary>
    /// Gets or sets the business hours calendar the clock runs in, or <see langword="null"/> for around the clock.
    /// </summary>
    public string HoursCalendarId { get; set; }

    /// <summary>
    /// Gets or sets the same opening hours every week the clock runs in, or <see langword="null"/>.
    /// </summary>
    public WeeklyHours WeeklyHours { get; set; }

    /// <summary>
    /// Gets or sets the time zone of the business hours.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets the statuses the clock stops in.
    /// </summary>
    public List<WorkOrderStatus> PauseStatuses { get; set; } = [];

    /// <summary>
    /// Gets or sets how far through a target, in percent, the warning is raised.
    /// </summary>
    public int WarningPercent { get; set; } = 80;

    /// <summary>
    /// Gets or sets when it was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}

/// <summary>
/// A finer status inside one, such as Waiting for parts inside On hold.
/// </summary>
public sealed class WorkOrderSubStatus : CatalogItem
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the status it belongs to.
    /// </summary>
    public WorkOrderStatus ParentStatus { get; set; }

    /// <summary>
    /// Gets or sets the color.
    /// </summary>
    public string Color { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the service level clock stops in it.
    /// </summary>
    public bool PausesSla { get; set; }
}
