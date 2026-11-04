namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// What Workforce keeps for a team, stored on the team itself. Read and change it with
/// <see cref="WorkforceTeamSettingsExtensions.GetWorkforceSettings"/> and
/// <see cref="WorkforceTeamSettingsExtensions.AlterWorkforceSettings"/>.
/// </summary>
public sealed class WorkforceTeamSettings
{
    /// <summary>
    /// Gets or sets where the team works, for a site.
    /// </summary>
    public WorkforceLocation Location { get; set; }

    /// <summary>
    /// Gets or sets the team's Workforce calendar, which holds its published shifts and time off.
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
}
