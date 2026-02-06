using System;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Models;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Elasticsearch;

/// <summary>
/// Contains metadata for Elasticsearch data sources.
/// </summary>
/// <remarks>
/// This class is obsolete. Use <see cref="AzureAIDataSourceIndexMetadata"/> for index configuration
/// on the AIDataSource and <see cref="AzureRagChatMetadata"/> for query-time parameters on the AIProfile.
/// </remarks>
[Obsolete($"Use {nameof(AzureAIDataSourceIndexMetadata)} for index configuration and {nameof(AzureRagChatMetadata)} for query-time parameters.")]
public sealed class AzureAIProfileElasticsearchMetadata
{
    public string IndexName { get; set; }

    public int? Strictness { get; set; }

    public int? TopNDocuments { get; set; }

    public string Filter { get; set; }
}
