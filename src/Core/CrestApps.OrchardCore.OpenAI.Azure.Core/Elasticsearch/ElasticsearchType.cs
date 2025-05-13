namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Elasticsearch;

/// <summary>
/// In OrchardCore v3 the options class was renamed from 'ElasticConnectionType' to 'ElasticsearchConnectionType'.
/// To ensure backward compatibility, the 'ElasticsearchType' was added to accommodate v2+.
/// </summary>
public enum ElasticsearchType
{
    SingleNodeConnectionPool,
    CloudConnectionPool,
    StaticConnectionPool,
    SniffingConnectionPool,
    StickyConnectionPool,
}
