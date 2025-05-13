namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Elasticsearch;

/// <summary>
/// In OrchardCore v3 the options class was renamed from 'ElasticConnectionOptions' to 'ElasticsearchConnectionOptions'.
/// To ensure backward compatibility, the 'ElasticsearchServerOptions' was added to accommodate v2+.
/// </summary>
public sealed class ElasticsearchServerOptions
{
    /// <summary>
    /// The server url.
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// The server connection port.
    /// </summary>
    public int[] Ports { get; set; }

    /// <summary>
    /// The server connection type.
    /// </summary>
    public ElasticsearchType ConnectionType { get; set; }

    /// <summary>
    /// The Elasticsearch cloud service CloudId.
    /// </summary>
    public string CloudId { get; set; }

    /// <summary>
    /// The server Username.
    /// </summary>
    public string Username { get; set; }

    /// <summary>
    /// The server Password.
    /// </summary>
    public string Password { get; set; }

    /// <summary>
    /// The server Certificate Fingerprint.
    /// </summary>
    public string CertificateFingerprint { get; set; }

    /// <summary>
    /// The index prefix.
    /// </summary>
    public string IndexPrefix { get; set; }

    public string AuthenticationType { get; set; }

    public string ApiKeyId { get; set; }

    public string ApiKey { get; set; }

    /// <summary>
    /// Whether the configuration section exists.
    /// </summary>
    private bool _fileConfigurationExists { get; set; }


    public void SetFileConfigurationExists(bool fileConfigurationExists)
        => _fileConfigurationExists = fileConfigurationExists;

    private bool? _isConfigured;

    public bool ConfigurationExists()
    {
        if (!_isConfigured.HasValue)
        {
            _isConfigured = !string.IsNullOrEmpty(Url) &&
                Uri.TryCreate(Url, UriKind.Absolute, out var _);
        }

        return _isConfigured.Value;
    }

    public bool FileConfigurationExists()
        => _fileConfigurationExists;
}
