namespace CrestApps.OrchardCore.SourceAccess;

/// <summary>
/// The records a person may see or act on, worked out once from every <see cref="ISourceAccess"/>: a record whose
/// source kind no source access restricts is allowed, and so is a record with no source kind, such as one written by
/// hand.
/// </summary>
public sealed class SourceAccessFilter
{
    /// <summary>
    /// The character that joins a source kind and an access scope in an access key.
    /// </summary>
    public const char KeySeparator = '/';

    /// <summary>
    /// Initializes a new instance of the <see cref="SourceAccessFilter"/> class.
    /// </summary>
    /// <param name="restrictedKinds">The source kinds whose records are not all allowed.</param>
    /// <param name="allowedKeys">The access keys (<see cref="KeyOf"/>) allowed within the restricted kinds.</param>
    public SourceAccessFilter(IEnumerable<string> restrictedKinds, IEnumerable<string> allowedKeys)
    {
        ArgumentNullException.ThrowIfNull(restrictedKinds);
        ArgumentNullException.ThrowIfNull(allowedKeys);

        RestrictedKinds = new HashSet<string>(restrictedKinds, StringComparer.Ordinal);
        AllowedKeys = new HashSet<string>(allowedKeys, StringComparer.Ordinal);
    }

    /// <summary>
    /// Gets a filter that allows every record.
    /// </summary>
    public static SourceAccessFilter Unrestricted { get; } = new([], []);

    /// <summary>
    /// Gets the source kinds whose records are not all allowed.
    /// </summary>
    public IReadOnlySet<string> RestrictedKinds { get; }

    /// <summary>
    /// Gets the access keys allowed within <see cref="RestrictedKinds"/>.
    /// </summary>
    public IReadOnlySet<string> AllowedKeys { get; }

    /// <summary>
    /// Gets a value indicating whether every record is allowed.
    /// </summary>
    public bool IsUnrestricted
        => RestrictedKinds.Count == 0;

    /// <summary>
    /// Returns the access key of a record, which an index keeps so a database query can apply the filter: its source
    /// kind and access scope, or <see langword="null"/> when it has no source kind or no access scope.
    /// </summary>
    /// <param name="sourceKind">The record's source kind.</param>
    /// <param name="accessScope">The record's access scope.</param>
    /// <returns>The key.</returns>
    public static string KeyOf(string sourceKind, string accessScope)
        => string.IsNullOrEmpty(sourceKind) || string.IsNullOrEmpty(accessScope)
            ? null
            : sourceKind + KeySeparator + accessScope;

    /// <summary>
    /// Returns whether the filter allows a record.
    /// </summary>
    /// <param name="sourceKind">The record's source kind.</param>
    /// <param name="accessScope">The record's access scope.</param>
    /// <returns><see langword="true"/> when the record is allowed.</returns>
    public bool Allows(string sourceKind, string accessScope)
    {
        if (string.IsNullOrEmpty(sourceKind) || !RestrictedKinds.Contains(sourceKind))
        {
            return true;
        }

        var key = KeyOf(sourceKind, accessScope);

        return key is not null && AllowedKeys.Contains(key);
    }
}
