using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.DataSources.AzureAI.ViewModels;

/// <summary>
/// View model for editing Azure AI Search AI data source settings.
/// </summary>
public class EditAzureAISearchAIDataSourceViewModel
{
    /// <summary>
    /// Gets or sets the Azure AI Search endpoint.
    /// </summary>
    public string Endpoint { get; set; }

    /// <summary>
    /// Gets or sets the authentication type.
    /// </summary>
    public string AuthenticationType { get; set; }

    /// <summary>
    /// Gets or sets the index name.
    /// </summary>
    public string IndexName { get; set; }

    /// <summary>
    /// Gets or sets the managed identity client identifier.
    /// </summary>
    public string IdentityClientId { get; set; }

    /// <summary>
    /// Gets or sets the API key.
    /// </summary>
    public string ApiKey { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an API key is already stored.
    /// </summary>
    [BindNever]
    public bool HasApiKey { get; set; }
}
