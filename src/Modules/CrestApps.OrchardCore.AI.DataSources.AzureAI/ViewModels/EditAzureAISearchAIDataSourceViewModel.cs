using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

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

    /// <summary>
    /// Gets or sets the identifier of the HTTP API data connection the data source uses, or empty when it uses its own
    /// endpoint and credentials.
    /// </summary>
    public string DataConnectionId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the current user may pick a data connection.
    /// </summary>
    [BindNever]
    public bool CanPickDataConnection { get; set; }

    /// <summary>
    /// Gets or sets the data connections the user may pick.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> DataConnections { get; set; } = [];
}
