namespace CrestApps.OrchardCore.Checks.Models;

/// <summary>
/// The checks a person may see or act on, worked out once from every <see cref="Services.ICheckSourceAccess"/>: a
/// check whose source kind no source access restricts is allowed, and so is a check written by hand.
/// </summary>
public sealed class CheckAccessFilter
{
    /// <summary>
    /// The character that joins a source kind and an access scope in an access key.
    /// </summary>
    public const char KeySeparator = '/';

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckAccessFilter"/> class.
    /// </summary>
    /// <param name="restrictedKinds">The source kinds whose checks are not all allowed.</param>
    /// <param name="allowedKeys">The access keys (<see cref="KeyOf"/>) allowed within the restricted kinds.</param>
    public CheckAccessFilter(IEnumerable<string> restrictedKinds, IEnumerable<string> allowedKeys)
    {
        ArgumentNullException.ThrowIfNull(restrictedKinds);
        ArgumentNullException.ThrowIfNull(allowedKeys);

        RestrictedKinds = new HashSet<string>(restrictedKinds, StringComparer.Ordinal);
        AllowedKeys = new HashSet<string>(allowedKeys, StringComparer.Ordinal);
    }

    /// <summary>
    /// Gets a filter that allows every check.
    /// </summary>
    public static CheckAccessFilter Unrestricted { get; } = new([], []);

    /// <summary>
    /// Gets the source kinds whose checks are not all allowed.
    /// </summary>
    public IReadOnlySet<string> RestrictedKinds { get; }

    /// <summary>
    /// Gets the access keys allowed within <see cref="RestrictedKinds"/>.
    /// </summary>
    public IReadOnlySet<string> AllowedKeys { get; }

    /// <summary>
    /// Gets a value indicating whether every check is allowed.
    /// </summary>
    public bool IsUnrestricted
        => RestrictedKinds.Count == 0;

    /// <summary>
    /// Returns the access key of a check: its source kind and access scope, or <see langword="null"/> when the check
    /// has no source kind or no access scope.
    /// </summary>
    /// <param name="sourceKind">The check's source kind.</param>
    /// <param name="accessScope">The check's access scope.</param>
    /// <returns>The key.</returns>
    public static string KeyOf(string sourceKind, string accessScope)
        => string.IsNullOrEmpty(sourceKind) || string.IsNullOrEmpty(accessScope)
            ? null
            : sourceKind + KeySeparator + accessScope;

    /// <summary>
    /// Returns whether the filter allows a check.
    /// </summary>
    /// <param name="check">The check.</param>
    /// <returns><see langword="true"/> when the check is allowed.</returns>
    public bool Allows(CheckRecord check)
    {
        ArgumentNullException.ThrowIfNull(check);

        if (string.IsNullOrEmpty(check.SourceKind) || !RestrictedKinds.Contains(check.SourceKind))
        {
            return true;
        }

        var key = KeyOf(check.SourceKind, check.AccessScope);

        return key is not null && AllowedKeys.Contains(key);
    }
}
