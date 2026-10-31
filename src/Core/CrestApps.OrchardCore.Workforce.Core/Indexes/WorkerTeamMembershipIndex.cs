using CrestApps.OrchardCore.Workforce.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// One row per team membership of a worker, so a team's workers are found without loading every worker.
/// </summary>
public sealed class WorkerTeamMembershipIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the worker id.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets the team id.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets what the worker does in the team.
    /// </summary>
    public TeamMemberRole Role { get; set; }

    /// <summary>
    /// Gets or sets the first day as <c>yyyyMMdd</c>, or <see langword="null"/>.
    /// </summary>
    public int? EffectiveFrom { get; set; }

    /// <summary>
    /// Gets or sets the last day as <c>yyyyMMdd</c>, or <see langword="null"/>.
    /// </summary>
    public int? EffectiveTo { get; set; }
}
