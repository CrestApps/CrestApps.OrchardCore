namespace CrestApps.AI.DataSources.AzureAI;

/// <summary>
/// Options for configuring an Azure AI Search connection.
/// Bind from configuration (e.g. "CrestApps:Search:AzureAISearch").
/// </summary>
public sealed class AzureAISearchConnectionOptions
{
    /// <summary>
    /// The Azure AI Search service endpoint (e.g. "https://my-search.search.windows.net").
    /// </summary>
    public string Endpoint { get; set; }

    /// <summary>
    /// The admin API key used for authentication.
    /// When empty, <c>DefaultAzureCredential</c> is used instead.
    /// </summary>
    public string ApiKey { get; set; }
}
