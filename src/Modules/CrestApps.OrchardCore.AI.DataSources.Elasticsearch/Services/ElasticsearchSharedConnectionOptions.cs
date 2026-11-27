namespace CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Services;

/// <summary>
/// Which indexes a tenant other than the default one may read through the globally configured Elasticsearch connection,
/// bound from the <c>CrestApps:AI:DataSources:Elasticsearch</c> section of the tenant's configuration, which only the
/// host's operator controls. The default tenant needs none.
/// </summary>
internal sealed class ElasticsearchSharedConnectionOptions
{
    /// <summary>
    /// The configuration section the options are bound from.
    /// </summary>
    public const string SectionName = "CrestApps:AI:DataSources:Elasticsearch";

    /// <summary>
    /// Gets or sets the index names a tenant other than the default one may read through the global connection. Empty by
    /// default, so such a tenant can't use the global connection.
    /// </summary>
    public List<string> SharedConnectionIndexes { get; set; } = [];
}
