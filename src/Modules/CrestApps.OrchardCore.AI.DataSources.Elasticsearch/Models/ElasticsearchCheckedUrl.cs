namespace CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Models;

/// <summary>
/// Records the self-managed URL a data source's save checked against the site's own network. A data source that has it
/// is held to the same rule when it connects; one without it was saved before the check (review 8, G7), and its cluster
/// may stay on a private network, though never on the link-local range or a cloud metadata service.
/// </summary>
internal sealed class ElasticsearchCheckedUrl
{
    /// <summary>
    /// Gets or sets the URL that was checked.
    /// </summary>
    public string Url { get; set; }
}
