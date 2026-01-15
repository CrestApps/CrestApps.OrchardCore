using CrestApps.OrchardCore.OpenAI.Azure.Core.Models;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core;

public sealed class AzureOpenAIDataSourceContext
{
    public AzureOpenAIDataSourceContext(string dataSourceId, string dataSourceType)
    {
        ArgumentNullException.ThrowIfNull(dataSourceId);
        ArgumentNullException.ThrowIfNull(dataSourceType);

        DataSourceId = dataSourceId;
        DataSourceType = dataSourceType;
    }

    public string DataSourceId { get; }

    public string DataSourceType { get; }

    /// <summary>
    /// Gets or sets the RAG query parameters from the AIProfile.
    /// When set, these values take precedence over legacy metadata on the data source.
    /// </summary>
    public AzureRagChatMetadata RagMetadata { get; set; }
}
