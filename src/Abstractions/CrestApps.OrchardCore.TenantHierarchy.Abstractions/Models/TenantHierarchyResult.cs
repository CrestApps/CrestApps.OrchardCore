namespace CrestApps.OrchardCore.TenantHierarchy.Models;

/// <summary>
/// Describes the outcome of a tenant hierarchy operation. Errors are generic on purpose, so a failure never reveals
/// whether another tenant exists.
/// </summary>
public sealed class TenantHierarchyResult
{
    private static readonly TenantHierarchyResult _success = new(null);

    private TenantHierarchyResult(string error)
    {
        Error = error;
    }

    /// <summary>
    /// Gets a value indicating whether the operation succeeded.
    /// </summary>
    public bool Succeeded => Error == null;

    /// <summary>
    /// Gets the error message, or <see langword="null"/> when the operation succeeded.
    /// </summary>
    public string Error { get; }

    /// <summary>
    /// Gets the result of an operation that succeeded.
    /// </summary>
    public static TenantHierarchyResult Success => _success;

    /// <summary>
    /// Creates the result of an operation that failed.
    /// </summary>
    /// <param name="error">The error message.</param>
    public static TenantHierarchyResult Failure(string error)
    {
        ArgumentException.ThrowIfNullOrEmpty(error);

        return new TenantHierarchyResult(error);
    }
}
