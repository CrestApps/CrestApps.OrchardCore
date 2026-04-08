namespace CrestApps.Core.Services;

/// <summary>
/// Validates whether the provided filter string conforms to basic OData syntax.
/// </summary>
public interface IODataValidator
{
    /// <summary>
    /// Determines whether the specified filter string is a valid OData filter expression.
    /// </summary>
    /// <param name="filter">The OData filter string to validate.</param>
    /// <returns><see langword="true"/> if the filter conforms to basic OData syntax; otherwise, <see langword="false"/>.</returns>
    bool IsValidFilter(string filter);
}
