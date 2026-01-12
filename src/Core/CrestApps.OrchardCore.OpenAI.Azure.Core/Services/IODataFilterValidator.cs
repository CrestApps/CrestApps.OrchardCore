namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Services;

/// <summary>
/// Provides basic OData filter syntax validation for Azure AI Search filters.
/// </summary>
public interface IODataFilterValidator
{
    /// <summary>
    /// Validates whether the provided filter string conforms to basic OData syntax.
    /// </summary>
    /// <param name="filter">The OData filter expression to validate.</param>
    /// <returns>True if the filter is valid or null/empty; otherwise, false.</returns>
    /// <remarks>
    /// This is a basic syntax validator that catches common errors.
    /// The Azure SDK performs full validation at runtime.
    /// </remarks>
    bool IsValid(string filter);
}
