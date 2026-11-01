using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Shifts of one worker to check against their labor rules.
/// </summary>
public sealed class LaborRuleRequest
{
    /// <summary>
    /// Gets or sets the worker.
    /// </summary>
    public WorkerProfile Worker { get; set; }

    /// <summary>
    /// Gets or sets the team the shifts are for.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the shifts being checked.
    /// </summary>
    public IList<LaborRuleShift> Candidates { get; set; } = [];

    /// <summary>
    /// Gets or sets the published shifts the candidates replace, which are left out.
    /// </summary>
    public ISet<string> IgnoredShiftKeys { get; set; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the changes to published shifts being made.
    /// </summary>
    public IList<LaborRuleChange> Changes { get; set; } = [];
}
