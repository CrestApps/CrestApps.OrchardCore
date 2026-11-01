using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// A part of the organization: a region, a site, a department, a team or a crew. Teams nest; a manager of a team also
/// manages its sub-teams. Each team gets a Workforce calendar that holds its published shifts and time off.
/// </summary>
public sealed class WorkforceTeam : CatalogItem, INameAwareModel, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the team name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets what level of the organization the team is.
    /// </summary>
    public TeamKind Kind { get; set; } = TeamKind.Team;

    /// <summary>
    /// Gets or sets the team it belongs to, or <see langword="null"/> at the top.
    /// </summary>
    public string ParentId { get; set; }

    /// <summary>
    /// Gets or sets what the team does.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets a role whose members can read the team's calendar.
    /// </summary>
    public string LinkedRoleName { get; set; }

    /// <summary>
    /// Gets or sets the team's time zone, which sets its day boundaries.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets where the team works, for a site.
    /// </summary>
    public WorkforceLocation Location { get; set; }

    /// <summary>
    /// Gets or sets the team's Workforce calendar.
    /// </summary>
    public string CalendarId { get; set; }

    /// <summary>
    /// Gets or sets the crew lead, for a crew.
    /// </summary>
    public string LeadWorkerId { get; set; }

    /// <summary>
    /// Gets or sets how the team's open shifts are given; <see langword="null"/> to use the parent team's.
    /// </summary>
    public OpenShiftClaimPolicy? OpenShiftClaimPolicy { get; set; }

    /// <summary>
    /// Gets or sets whether a manager approves shift trades once a colleague accepts; <see langword="null"/> to use
    /// the parent team's.
    /// </summary>
    public bool? ShiftTradesNeedApproval { get; set; }

    /// <summary>
    /// Gets or sets what happens to a worker's published shifts when their time off is approved; <see langword="null"/>
    /// to use the parent team's.
    /// </summary>
    public TimeOffShiftHandling? TimeOffShiftHandling { get; set; }

    /// <summary>
    /// Gets or sets the time clock policy; <see langword="null"/> to use the parent team's.
    /// </summary>
    public string TimeClockPolicyId { get; set; }

    /// <summary>
    /// Gets or sets whether the team is in use.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the UTC time the team was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the team was last changed.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}
