using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// A schedule about to be published, for <see cref="ISchedulePublishValidator"/>s to check.
/// </summary>
public sealed class SchedulePublishValidationContext
{
    /// <summary>
    /// Gets or sets the schedule.
    /// </summary>
    public SchedulePeriod Period { get; set; }

    /// <summary>
    /// Gets or sets the team's time zone.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets the draft shifts that would be published, with their workers.
    /// </summary>
    public IReadOnlyList<DraftShift> Drafts { get; set; } = [];

    /// <summary>
    /// Gets or sets the workers of the drafts, by id.
    /// </summary>
    public IReadOnlyDictionary<string, WorkerProfile> Workers { get; set; } = new Dictionary<string, WorkerProfile>();

    /// <summary>
    /// Gets or sets the keys of the schedule's shifts already published.
    /// </summary>
    public ISet<string> PublishedShiftKeys { get; set; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the changes the publish would make to published shifts; empty for a first publish.
    /// </summary>
    public IReadOnlyList<SchedulePublishChange> Changes { get; set; } = [];

    /// <summary>
    /// Gets or sets the reason given to go ahead despite rules that need an override; empty when none was given.
    /// </summary>
    public string OverrideReason { get; set; }

    /// <summary>
    /// Gets or sets who publishes.
    /// </summary>
    public string Actor { get; set; }

    /// <summary>
    /// Gets or sets whether the schedule is only being checked, so nothing should be recorded.
    /// </summary>
    public bool CheckOnly { get; set; }

    /// <summary>
    /// Gets the broken rules found.
    /// </summary>
    public IList<LaborRuleViolation> Violations { get; } = [];
}
