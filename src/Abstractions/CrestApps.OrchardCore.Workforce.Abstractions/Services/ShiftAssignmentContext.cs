using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// A worker about to be given a published shift, for <see cref="IShiftAssignmentValidator"/>s to check.
/// </summary>
public sealed class ShiftAssignmentContext
{
    /// <summary>
    /// Gets or sets the worker.
    /// </summary>
    public WorkerProfile Worker { get; set; }

    /// <summary>
    /// Gets or sets the shift's team.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the day the shift belongs to.
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// Gets or sets when the shift starts.
    /// </summary>
    public DateTime StartUtc { get; set; }

    /// <summary>
    /// Gets or sets when it ends.
    /// </summary>
    public DateTime EndUtc { get; set; }

    /// <summary>
    /// Gets or sets its paid minutes.
    /// </summary>
    public int PaidMinutes { get; set; }

    /// <summary>
    /// Gets or sets the minutes of breaks and meals inside the shift.
    /// </summary>
    public int BreakMinutes { get; set; }

    /// <summary>
    /// Gets or sets the skills it needs.
    /// </summary>
    public IReadOnlyCollection<string> RequiredSkillIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the keys of the worker's shifts that leave them in the same change, such as the shift they give
    /// in a swap, and the key of the shift itself.
    /// </summary>
    public ISet<string> IgnoredShiftKeys { get; set; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// Gets the reasons the worker can't take the shift.
    /// </summary>
    public IList<string> Problems { get; } = [];
}
