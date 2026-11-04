using YesSql.Indexes;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// One row for each team or worker a time-off policy is assigned to, to find the policies that apply to a worker.
/// </summary>
public sealed class TimeOffPolicyAssignmentIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the policy.
    /// </summary>
    public string PolicyId { get; set; }

    /// <summary>
    /// Gets or sets the team, for a team assignment.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the worker, for a worker assignment.
    /// </summary>
    public string WorkerId { get; set; }
}
