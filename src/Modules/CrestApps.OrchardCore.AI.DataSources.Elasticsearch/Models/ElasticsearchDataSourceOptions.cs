using CrestApps.Core.AI.Models;

namespace CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Models;

/// <summary>
/// Provides the global connection configuration for Elasticsearch AI data sources.
/// </summary>
public sealed class ElasticsearchDataSourceOptions
{
    /// <summary>
    /// Gets or sets the Elasticsearch server URL.
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// Gets or sets the Elastic Cloud deployment identifier.
    /// </summary>
    public string CloudId { get; set; }

    /// <summary>
    /// Gets or sets the authentication type.
    /// </summary>
    public string AuthenticationType { get; set; }

    /// <summary>
    /// Gets or sets the username for basic authentication.
    /// </summary>
    public string Username { get; set; }

    /// <summary>
    /// Gets or sets the password for basic authentication.
    /// </summary>
    public string Password { get; set; }

    /// <summary>
    /// Gets or sets the API key.
    /// </summary>
    public string ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the base64-encoded API key.
    /// </summary>
    public string Base64ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the API key identifier.
    /// </summary>
    public string ApiKeyId { get; set; }

    /// <summary>
    /// Gets or sets the certificate fingerprint used for TLS validation.
    /// </summary>
    public string CertificateFingerprint { get; set; }

    /// <summary>
    /// Gets a value indicating whether a connection is configured.
    /// </summary>
    public bool HasConnection
        => !string.IsNullOrWhiteSpace(Url) || !string.IsNullOrWhiteSpace(CloudId);

    /// <summary>
    /// Gets the environment type implied by the configured values.
    /// </summary>
    public string GetEnvironmentType()
        => string.IsNullOrWhiteSpace(CloudId)
            ? ElasticsearchSourceMetadata.SelfManagedEnvironmentType
            : ElasticsearchSourceMetadata.CloudHostedEnvironmentType;

    /// <summary>
    /// Gets the normalized authentication type, inferred from the configured credentials when it is
    /// not set explicitly.
    /// </summary>
    public string GetAuthenticationType()
    {
        if (string.IsNullOrWhiteSpace(AuthenticationType))
        {
            if (!string.IsNullOrWhiteSpace(Username) || !string.IsNullOrWhiteSpace(Password))
            {
                return ElasticsearchSourceMetadata.BasicAuthenticationType;
            }

            if (!string.IsNullOrWhiteSpace(ApiKeyId))
            {
                return ElasticsearchSourceMetadata.KeyIdAndKeyAuthenticationType;
            }

            if (!string.IsNullOrWhiteSpace(ApiKey))
            {
                return ElasticsearchSourceMetadata.ApiKeyAuthenticationType;
            }

            if (!string.IsNullOrWhiteSpace(Base64ApiKey))
            {
                return ElasticsearchSourceMetadata.Base64ApiKeyAuthenticationType;
            }

            return ElasticsearchSourceMetadata.NoneAuthenticationType;
        }

        var authenticationType = AuthenticationType.Trim();

        if (string.Equals(authenticationType, ElasticsearchSourceMetadata.BasicAuthenticationType, StringComparison.OrdinalIgnoreCase))
        {
            return ElasticsearchSourceMetadata.BasicAuthenticationType;
        }

        if (string.Equals(authenticationType, ElasticsearchSourceMetadata.ApiKeyAuthenticationType, StringComparison.OrdinalIgnoreCase))
        {
            return ElasticsearchSourceMetadata.ApiKeyAuthenticationType;
        }

        if (string.Equals(authenticationType, ElasticsearchSourceMetadata.Base64ApiKeyAuthenticationType, StringComparison.OrdinalIgnoreCase))
        {
            return ElasticsearchSourceMetadata.Base64ApiKeyAuthenticationType;
        }

        if (string.Equals(authenticationType, ElasticsearchSourceMetadata.KeyIdAndKeyAuthenticationType, StringComparison.OrdinalIgnoreCase))
        {
            return ElasticsearchSourceMetadata.KeyIdAndKeyAuthenticationType;
        }

        return ElasticsearchSourceMetadata.NoneAuthenticationType;
    }
}
