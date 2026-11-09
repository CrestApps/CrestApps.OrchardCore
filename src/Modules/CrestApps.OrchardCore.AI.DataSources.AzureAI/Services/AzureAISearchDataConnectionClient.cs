using Azure;
using Azure.Identity;
using Azure.Search.Documents;
using CrestApps.OrchardCore.DataConnectors;
using CrestApps.OrchardCore.DataConnectors.Http;

namespace CrestApps.OrchardCore.AI.DataSources.AzureAI.Services;

/// <summary>
/// Creates the Azure AI Search client of an AI data source from the HTTP API data connection it references: the
/// connection's base URL is the search service endpoint, and its API key (sent in any header, usually <c>api-key</c>)
/// is the admin or query key. A connection without authentication uses the default Azure credential, such as the
/// site's managed identity.
/// </summary>
internal static class AzureAISearchDataConnectionClient
{
    /// <summary>
    /// The connectors whose connections an Azure AI Search AI data source can use.
    /// </summary>
    public static readonly IReadOnlyCollection<string> ConnectorNames = [DataConnectorConstants.Connectors.HttpApi];

    /// <summary>
    /// Creates the client.
    /// </summary>
    /// <param name="settings">The connection's HTTP settings.</param>
    /// <param name="secrets">The connection's secrets.</param>
    /// <param name="indexName">The index the data source reads.</param>
    /// <returns>The client.</returns>
    /// <exception cref="InvalidOperationException">The connection has no URL, no API key, or an authentication Azure AI Search cannot use.</exception>
    public static SearchClient Create(HttpConnectionSettings settings, IDataConnectorSecrets secrets, string indexName)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(secrets);

        if (!Uri.TryCreate(settings.BaseUrl?.Trim(), UriKind.Absolute, out var endpoint))
        {
            throw new InvalidOperationException("The data connection has no valid Azure AI Search endpoint.");
        }

        var authentication = settings.Authentication ?? new HttpAuthenticationSettings();

        switch (authentication.Type)
        {
            case HttpAuthenticationType.ApiKey:
                var apiKey = secrets.Get(HttpAuthenticationSecrets.ApiKey);

                if (string.IsNullOrEmpty(apiKey))
                {
                    throw new InvalidOperationException("The data connection has no API key.");
                }

                return new SearchClient(endpoint, indexName, new AzureKeyCredential(apiKey));

            case HttpAuthenticationType.None:
                return new SearchClient(endpoint, indexName, new DefaultAzureCredential());

            default:
                throw new InvalidOperationException($"The data connection uses {authentication.Type} authentication, which an Azure AI Search AI data source cannot use. Use an API key, or no authentication for the default Azure credential.");
        }
    }
}
