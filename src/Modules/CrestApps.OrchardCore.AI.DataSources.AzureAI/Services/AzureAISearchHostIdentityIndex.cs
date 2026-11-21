namespace CrestApps.OrchardCore.AI.DataSources.AzureAI.Services;

/// <summary>
/// A search service, index and identity the host's operator lets a tenant read as the host.
/// </summary>
internal sealed class AzureAISearchHostIdentityIndex
{
    /// <summary>
    /// Gets or sets the search service endpoint, compared by scheme, host and port.
    /// </summary>
    public string Endpoint { get; set; }

    /// <summary>
    /// Gets or sets the index name.
    /// </summary>
    public string IndexName { get; set; }

    /// <summary>
    /// Gets or sets the client ID of the user-assigned managed identity, or nothing for the system-assigned identity, the
    /// default credential, or a data connection without authentication.
    /// </summary>
    public string IdentityClientId { get; set; }
}
