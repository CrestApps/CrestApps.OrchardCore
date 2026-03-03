namespace CrestApps.Services;

/// <summary>
/// Validates whether the provided filter string conforms to basic OData syntax.
/// </summary>
public interface IODataValidator
{
    bool IsValidFilter(string filter);
}
