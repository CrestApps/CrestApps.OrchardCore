using CrestApps.Core.AI.Models;

namespace CrestApps.OrchardCore.AI.DataSources.Drivers;

internal static class AIDataSourceDriverHelper
{
    public static string GetSourceType(AIDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);

        return string.IsNullOrWhiteSpace(dataSource.SourceType)
            ? AIDataSourceSourceTypes.SearchIndexProfile
            : dataSource.SourceType;
    }

    public static bool IsConfigurationLocked(AIDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);

        return !string.IsNullOrWhiteSpace(dataSource.AIKnowledgeBaseIndexProfileName) &&
            !string.IsNullOrWhiteSpace(dataSource.ContentFieldName) &&
            (!string.Equals(
                GetSourceType(dataSource),
                AIDataSourceSourceTypes.SearchIndexProfile,
                StringComparison.OrdinalIgnoreCase) ||
                !string.IsNullOrWhiteSpace(dataSource.SourceIndexProfileName));
    }
}
