using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Services;

/// <summary>
/// Decides which indexes a data source may read through the globally configured Elasticsearch connection. That connection
/// carries the host's credentials and reaches whatever they can, other tenants' indexes included, so, as an Azure AI
/// Search data source's sign-in as the host, it works on the default tenant, and on another tenant only for an index
/// listed in <see cref="ElasticsearchSharedConnectionOptions"/> (review 3).
/// </summary>
internal sealed class ElasticsearchSharedConnectionAccess
{
    /// <summary>
    /// The error shown when a data source reads an index through the global connection where it may not.
    /// </summary>
    public const string NotApprovedError = "The globally configured Elasticsearch connection is only available on the default tenant, or for an index the host approved for this tenant. Enter this data source's own connection settings, or ask the host to approve this index.";

    private readonly bool _isDefaultTenant;
    private readonly List<string> _approved;

    /// <summary>
    /// Initializes a new instance of the <see cref="ElasticsearchSharedConnectionAccess"/> class.
    /// </summary>
    /// <param name="shellSettings">The settings of the current tenant.</param>
    /// <param name="options">The host's approvals for this tenant.</param>
    public ElasticsearchSharedConnectionAccess(ShellSettings shellSettings, IOptions<ElasticsearchSharedConnectionOptions> options)
    {
        _isDefaultTenant = shellSettings?.IsDefaultShell() == true;
        _approved = options?.Value?.SharedConnectionIndexes ?? [];
    }

    /// <summary>
    /// Whether this tenant may read the index through the global connection. The name is compared whole, so a pattern or
    /// a list of indexes is allowed only when the host listed that exact text.
    /// </summary>
    /// <param name="indexName">The index name.</param>
    public bool IsApproved(string indexName)
        => _isDefaultTenant || (!string.IsNullOrWhiteSpace(indexName) && _approved.Any(entry =>
            !string.IsNullOrWhiteSpace(entry) &&
            string.Equals(entry.Trim(), indexName.Trim(), StringComparison.OrdinalIgnoreCase)));
}
