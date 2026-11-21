namespace CrestApps.OrchardCore.AI.DataSources.AzureAI.Services;

/// <summary>
/// Host settings for Azure AI Search data sources that sign in as the host (a managed identity, the default Azure
/// credential, or a data connection without authentication), bound from the <c>CrestApps:AI:DataSources:AzureAISearch</c>
/// section of the tenant's configuration, which only the host's operator controls. The default tenant needs none.
/// </summary>
internal sealed class AzureAISearchHostIdentityOptions
{
    /// <summary>
    /// The configuration section the options are bound from.
    /// </summary>
    public const string SectionName = "CrestApps:AI:DataSources:AzureAISearch";

    /// <summary>
    /// Gets or sets the search services, indexes and identities a tenant other than the default one may read as the host.
    /// Empty by default, so such a tenant can't sign in as the host.
    /// </summary>
    public List<AzureAISearchHostIdentityIndex> HostIdentityIndexes { get; set; } = [];
}
