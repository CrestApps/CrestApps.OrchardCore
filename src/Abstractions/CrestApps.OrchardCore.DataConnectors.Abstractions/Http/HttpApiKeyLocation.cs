namespace CrestApps.OrchardCore.DataConnectors.Http;

/// <summary>
/// Where an API key is sent.
/// </summary>
public enum HttpApiKeyLocation
{
    /// <summary>
    /// In a request header.
    /// </summary>
    Header,

    /// <summary>
    /// In a query string parameter.
    /// </summary>
    Query,
}
