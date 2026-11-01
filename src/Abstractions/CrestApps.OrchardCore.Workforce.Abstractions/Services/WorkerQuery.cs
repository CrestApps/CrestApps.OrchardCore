using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Which workers to list.
/// </summary>
public sealed class WorkerQuery
{
    /// <summary>
    /// Gets or sets a text the name, email or employee number must contain.
    /// </summary>
    public string Search { get; set; }

    /// <summary>
    /// Gets or sets a team the worker must belong to, including its sub-teams.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the status, or <see langword="null"/> for every status.
    /// </summary>
    public WorkerStatus? Status { get; set; }

    /// <summary>
    /// Gets or sets the teams to limit the list to, as a manager's scope does; <see langword="null"/> for every team.
    /// </summary>
    public IReadOnlyCollection<string> TeamIds { get; set; }

    /// <summary>
    /// Gets or sets whether to list only workers linked to a user (<see langword="true"/>) or only those who aren't
    /// (<see langword="false"/>); <see langword="null"/> for both.
    /// </summary>
    public bool? HasUser { get; set; }
}
