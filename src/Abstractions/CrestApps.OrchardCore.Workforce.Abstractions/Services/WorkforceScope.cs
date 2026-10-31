namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// What part of the workforce a user may work with.
/// </summary>
public sealed class WorkforceScope
{
    /// <summary>
    /// A scope that covers nothing.
    /// </summary>
    public static readonly WorkforceScope None = new(false, new HashSet<string>(StringComparer.Ordinal));

    /// <summary>
    /// A scope that covers every team.
    /// </summary>
    public static readonly WorkforceScope Everything = new(true, new HashSet<string>(StringComparer.Ordinal));

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkforceScope"/> class.
    /// </summary>
    /// <param name="all">Whether the scope covers every team.</param>
    /// <param name="teamIds">The teams it covers when it doesn't cover all of them.</param>
    public WorkforceScope(bool all, IReadOnlySet<string> teamIds)
    {
        All = all;
        TeamIds = teamIds ?? new HashSet<string>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Gets whether the scope covers every team.
    /// </summary>
    public bool All { get; }

    /// <summary>
    /// Gets the teams the scope covers, with their sub-teams, when it doesn't cover all of them.
    /// </summary>
    public IReadOnlySet<string> TeamIds { get; }

    /// <summary>
    /// Gets whether the scope covers a team.
    /// </summary>
    /// <param name="teamId">The team.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    public bool Covers(string teamId)
    {
        return All || (!string.IsNullOrEmpty(teamId) && TeamIds.Contains(teamId));
    }
}
