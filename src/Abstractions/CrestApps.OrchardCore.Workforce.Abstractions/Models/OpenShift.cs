using System.Text.Json.Serialization;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Calendars.Serialization;

namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// A shift with nobody yet, offered to the team's eligible workers. Publishing a schedule posts its shifts that have
/// nobody; a manager can also post one on its own.
/// </summary>
public sealed class OpenShift : CatalogItem
{
    /// <summary>
    /// Gets or sets the team.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the schedule it came from; <see langword="null"/> for one posted on its own.
    /// </summary>
    public string PeriodId { get; set; }

    /// <summary>
    /// Gets or sets the key the shift keeps on the calendar once filled, the draft shift's key for a scheduled one.
    /// </summary>
    public string ShiftKey { get; set; }

    /// <summary>
    /// Gets or sets the day the shift belongs to.
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// Gets or sets when it starts, on the team's wall clock.
    /// </summary>
    [JsonConverter(typeof(WallClockDateTimeJsonConverter))]
    public DateTime Start { get; set; }

    /// <summary>
    /// Gets or sets when it ends, on the team's wall clock.
    /// </summary>
    [JsonConverter(typeof(WallClockDateTimeJsonConverter))]
    public DateTime End { get; set; }

    /// <summary>
    /// Gets or sets the team's time zone.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets the template it was made from.
    /// </summary>
    public string TemplateId { get; set; }

    /// <summary>
    /// Gets or sets its breaks and activities.
    /// </summary>
    public IList<DraftShiftSegment> Segments { get; set; } = [];

    /// <summary>
    /// Gets or sets the skills a worker needs to take it.
    /// </summary>
    public IList<string> RequiredSkillIds { get; set; } = [];

    /// <summary>
    /// Gets or sets notes shown with it.
    /// </summary>
    public string Notes { get; set; }

    /// <summary>
    /// Gets or sets how it is given to claimants.
    /// </summary>
    public OpenShiftClaimPolicy ClaimPolicy { get; set; }

    /// <summary>
    /// Gets or sets when it is awarded, for <see cref="OpenShiftClaimPolicy.BySeniority"/>.
    /// </summary>
    public DateTime? ClaimDeadlineUtc { get; set; }

    /// <summary>
    /// Gets or sets where it stands.
    /// </summary>
    public OpenShiftStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the claims made on it.
    /// </summary>
    public IList<OpenShiftClaim> Claims { get; set; } = [];

    /// <summary>
    /// Gets or sets the worker who got it.
    /// </summary>
    public string FilledWorkerId { get; set; }

    /// <summary>
    /// Gets or sets when it was filled.
    /// </summary>
    public DateTime? FilledUtc { get; set; }

    /// <summary>
    /// Gets or sets the calendar event made when it was filled.
    /// </summary>
    public string CalendarEventId { get; set; }

    /// <summary>
    /// Gets or sets who posted it.
    /// </summary>
    public string CreatedBy { get; set; }

    /// <summary>
    /// Gets or sets when it was posted.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}
