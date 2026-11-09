using System.Collections.Specialized;
using CrestApps.OrchardCore.DataConnectors;
using CrestApps.OrchardCore.DataConnectors.Http;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;

namespace CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Services;

/// <summary>
/// Builds the Elasticsearch client settings of an AI data source from the Elasticsearch data connection it references:
/// the connection's base URL, authentication and extra headers, with the secrets read from the connection.
/// </summary>
internal static class ElasticsearchDataConnectionSettings
{
    /// <summary>
    /// The header that carries credentials.
    /// </summary>
    private const string AuthorizationHeaderName = "Authorization";

    /// <summary>
    /// The connectors whose connections an Elasticsearch AI data source can use.
    /// </summary>
    public static readonly IReadOnlyCollection<string> ConnectorNames = [DataConnectorConstants.Connectors.Elasticsearch];

    /// <summary>
    /// Creates the client settings.
    /// </summary>
    /// <param name="settings">The connection's HTTP settings.</param>
    /// <param name="secrets">The connection's secrets.</param>
    /// <returns>The client settings.</returns>
    /// <exception cref="InvalidOperationException">The connection has no URL, misses a secret, or uses an authentication the Elasticsearch client cannot send.</exception>
    public static ElasticsearchClientSettings Create(HttpConnectionSettings settings, IDataConnectorSecrets secrets)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(secrets);

        if (!Uri.TryCreate(settings.BaseUrl?.Trim(), UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException("The data connection has no valid Elasticsearch URL.");
        }

        var clientSettings = new ElasticsearchClientSettings(uri);
        var headers = new NameValueCollection();
        var authentication = settings.Authentication ?? new HttpAuthenticationSettings();

        switch (authentication.Type)
        {
            case HttpAuthenticationType.None:
                break;

            case HttpAuthenticationType.Basic:
                clientSettings = clientSettings.Authentication(new BasicAuthentication(authentication.UserName, Required(secrets, HttpAuthenticationSecrets.Password, "password")));
                break;

            case HttpAuthenticationType.Bearer:
                clientSettings = clientSettings.Authentication(new SchemeAuthorizationHeader("Bearer", Required(secrets, HttpAuthenticationSecrets.BearerToken, "bearer token")));
                break;

            case HttpAuthenticationType.ApiKey when authentication.ApiKeyLocation == HttpApiKeyLocation.Header:
                var apiKey = Required(secrets, HttpAuthenticationSecrets.ApiKey, "API key");
                var name = string.IsNullOrWhiteSpace(authentication.ApiKeyName) ? AuthorizationHeaderName : authentication.ApiKeyName.Trim();

                if (string.Equals(name, AuthorizationHeaderName, StringComparison.OrdinalIgnoreCase))
                {
                    // Elasticsearch's own API keys travel as "Authorization: ApiKey <key>", which is what the connection
                    // editor proposes for a new Elasticsearch connection.
                    var scheme = string.IsNullOrWhiteSpace(authentication.ApiKeyPrefix) ? "ApiKey" : authentication.ApiKeyPrefix.Trim();

                    clientSettings = clientSettings.Authentication(new SchemeAuthorizationHeader(scheme, apiKey));
                }
                else
                {
                    headers.Add(name, (authentication.ApiKeyPrefix ?? string.Empty) + apiKey);
                }

                break;

            default:
                throw new InvalidOperationException($"The data connection uses {authentication.Type} authentication, which an Elasticsearch AI data source cannot use. Use no authentication, Basic, a bearer token or an API key in a header.");
        }

        foreach (var header in settings.Headers ?? [])
        {
            if (!string.IsNullOrWhiteSpace(header?.Name))
            {
                headers.Add(header.Name.Trim(), header.Value ?? string.Empty);
            }
        }

        if (headers.Count > 0)
        {
            clientSettings = clientSettings.GlobalHeaders(headers);
        }

        if (settings.TimeoutSeconds > 0)
        {
            clientSettings = clientSettings.RequestTimeout(TimeSpan.FromSeconds(settings.TimeoutSeconds));
        }

        return clientSettings;
    }

    private static string Required(IDataConnectorSecrets secrets, string name, string label)
    {
        var value = secrets.Get(name);

        return string.IsNullOrEmpty(value)
            ? throw new InvalidOperationException($"The data connection has no {label}.")
            : value;
    }
}
