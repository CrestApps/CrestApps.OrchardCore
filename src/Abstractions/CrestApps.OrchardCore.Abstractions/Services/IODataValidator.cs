namespace CrestApps.OrchardCore.Services;

/// <summary>
/// Provides basic OData filter syntax validation.
/// </summary>
public interface IODataValidator
{
    /// <summary>
    /// Validates whether the provided filter string conforms to basic OData syntax.
    /// </summary>
    /// <param name="filter">The OData filter expression to validate.</param>
    /// <returns>True if the filter is valid or null/empty; otherwise, false.</returns>
    /// <remarks>
    /// This is a basic syntax validator that catches common errors.
    /// Full validation is performed by the underlying service at runtime.
    /// </remarks>
    bool IsValidFilter(string filter);
}
