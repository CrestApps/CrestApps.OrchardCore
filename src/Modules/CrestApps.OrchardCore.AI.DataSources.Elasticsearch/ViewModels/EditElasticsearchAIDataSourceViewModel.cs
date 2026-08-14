using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.DataSources.Elasticsearch.ViewModels;

/// <summary>
/// View model for editing Elasticsearch AI data source settings.
/// </summary>
public class EditElasticsearchAIDataSourceViewModel
{
    /// <summary>
    /// Gets or sets the Elasticsearch environment type.
    /// </summary>
    public string EnvironmentType { get; set; }

    /// <summary>
    /// Gets or sets the Elasticsearch endpoint URL.
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
    /// Gets or sets the Elasticsearch index name.
    /// </summary>
    public string IndexName { get; set; }

    /// <summary>
    /// Gets or sets the username for basic authentication.
    /// </summary>
    public string Username { get; set; }

    /// <summary>
    /// Gets or sets the password for basic authentication.
    /// </summary>
    public string Password { get; set; }

    /// <summary>
    /// Gets or sets the Elasticsearch API key.
    /// </summary>
    public string ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the base64-encoded Elasticsearch API key.
    /// </summary>
    public string Base64ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the Elasticsearch API key identifier.
    /// </summary>
    public string ApiKeyId { get; set; }

    /// <summary>
    /// Gets or sets the certificate fingerprint.
    /// </summary>
    public string CertificateFingerprint { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a password is already stored.
    /// </summary>
    [BindNever]
    public bool HasPassword { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an API key is already stored.
    /// </summary>
    [BindNever]
    public bool HasApiKey { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a base64 API key is already stored.
    /// </summary>
    [BindNever]
    public bool HasBase64ApiKey { get; set; }
}
