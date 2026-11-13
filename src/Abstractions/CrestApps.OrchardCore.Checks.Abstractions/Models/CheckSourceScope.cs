namespace CrestApps.OrchardCore.Checks.Models;

/// <summary>
/// The checks of one source kind a person may see or act on: all of them, none, or those of some access scopes.
/// </summary>
public sealed class CheckSourceScope
{
    private CheckSourceScope(bool all, IEnumerable<string> scopes)
    {
        IsAll = all;
        Scopes = new HashSet<string>(scopes.Where(scope => !string.IsNullOrEmpty(scope)), StringComparer.Ordinal);
    }

    /// <summary>
    /// Gets a scope that allows every check of the source kind.
    /// </summary>
    public static CheckSourceScope All { get; } = new(true, []);

    /// <summary>
    /// Gets a scope that allows no check of the source kind.
    /// </summary>
    public static CheckSourceScope None { get; } = new(false, []);

    /// <summary>
    /// Gets a value indicating whether every check of the source kind is allowed.
    /// </summary>
    public bool IsAll { get; }

    /// <summary>
    /// Gets the access scopes (<see cref="CheckRecord.AccessScope"/>) whose checks are allowed, when not all are.
    /// </summary>
    public IReadOnlySet<string> Scopes { get; }

    /// <summary>
    /// Creates a scope that allows the checks of some access scopes.
    /// </summary>
    /// <param name="scopes">The access scopes, such as the pay groups a person runs payroll for.</param>
    /// <returns>The scope.</returns>
    public static CheckSourceScope Of(IEnumerable<string> scopes)
    {
        ArgumentNullException.ThrowIfNull(scopes);

        return new CheckSourceScope(false, scopes);
    }
}
