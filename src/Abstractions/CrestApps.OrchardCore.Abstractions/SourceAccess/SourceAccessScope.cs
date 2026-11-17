namespace CrestApps.OrchardCore.SourceAccess;

/// <summary>
/// The records of one source kind a person may see or act on: all of them, none, or those of some access scopes.
/// </summary>
public sealed class SourceAccessScope
{
    private SourceAccessScope(bool all, IEnumerable<string> scopes)
    {
        IsAll = all;
        Scopes = new HashSet<string>(scopes.Where(scope => !string.IsNullOrEmpty(scope)), StringComparer.Ordinal);
    }

    /// <summary>
    /// Gets a scope that allows every record of the source kind.
    /// </summary>
    public static SourceAccessScope All { get; } = new(true, []);

    /// <summary>
    /// Gets a scope that allows no record of the source kind.
    /// </summary>
    public static SourceAccessScope None { get; } = new(false, []);

    /// <summary>
    /// Gets a value indicating whether every record of the source kind is allowed.
    /// </summary>
    public bool IsAll { get; }

    /// <summary>
    /// Gets the access scopes whose records are allowed, when not all are.
    /// </summary>
    public IReadOnlySet<string> Scopes { get; }

    /// <summary>
    /// Creates a scope that allows the records of some access scopes.
    /// </summary>
    /// <param name="scopes">The access scopes, such as the employers a person runs payroll for.</param>
    /// <returns>The scope.</returns>
    public static SourceAccessScope Of(IEnumerable<string> scopes)
    {
        ArgumentNullException.ThrowIfNull(scopes);

        return new SourceAccessScope(false, scopes);
    }
}
