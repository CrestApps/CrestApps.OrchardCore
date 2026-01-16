using System;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Models;

/// <summary>
/// Contains metadata for Azure AI Search data sources.
/// </summary>
/// <remarks>
/// This class is obsolete. Use <see cref="AzureAIDataSourceIndexMetadata"/> for index configuration
/// on the AIDataSource and <see cref="AzureRagChatMetadata"/> for query-time parameters on the AIProfile.
/// </remarks>
[Obsolete($"Use {nameof(AzureAIDataSourceIndexMetadata)} for index configuration and {nameof(AzureRagChatMetadata)} for query-time parameters.")]
public sealed class AzureAIProfileAISearchMetadata
{
    public string IndexName { get; set; }

    public int? Strictness { get; set; }

    public int? TopNDocuments { get; set; }

    public string Filter { get; set; }
}
