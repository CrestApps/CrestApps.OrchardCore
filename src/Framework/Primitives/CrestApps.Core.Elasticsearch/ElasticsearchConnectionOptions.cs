namespace CrestApps.Core.Elasticsearch;

/// <summary>
/// Options for configuring an Elasticsearch connection.
/// Bind from configuration (e.g. "CrestApps:Elasticsearch").
/// </summary>
public sealed class ElasticsearchConnectionOptions
{
    /// <summary>
    /// The Elasticsearch server URL (e.g. "https://localhost:9200").
    /// </summary>
    public string Url { get; set; }
    /// <summary>
    /// Optional username for basic authentication.
    /// </summary>
    public string Username { get; set; }
    /// <summary>
    /// Optional password for basic authentication.
    /// </summary>
    public string Password { get; set; }
    /// <summary>
    /// Optional certificate fingerprint for TLS verification.
    /// </summary>
    public string CertificateFingerprint { get; set; }

    /// <summary>
    /// Optional prefix applied to MVC-managed remote index names.
    /// </summary>
    public string IndexPrefix { get; set; }
}
