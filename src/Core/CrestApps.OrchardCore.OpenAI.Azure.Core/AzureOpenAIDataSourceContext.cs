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
}
