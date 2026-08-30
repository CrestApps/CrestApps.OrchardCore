using CrestApps.Core.AI.Models;

namespace CrestApps.OrchardCore.AI.DataSources.Drivers;

internal static class AIDataSourceDriverHelper
{
    public static string GetSourceType(AIDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);

        return string.IsNullOrWhiteSpace(dataSource.Source)
            ? AIDataSourceSourceTypes.SearchIndexProfile
            : dataSource.Source;
    }

    /// <summary>
    /// Returns whether the given source type needs the shared field mapping (content, title, key). Sources
    /// that produce fully-formed documents themselves — such as the <see cref="AIDataSourceSourceTypes.Web"/>
    /// source — supply the title, content, and reference key directly, so the mapping does not apply.
    /// </summary>
    public static bool RequiresFieldMapping(string sourceType)
        => !string.Equals(sourceType, AIDataSourceSourceTypes.Web, StringComparison.OrdinalIgnoreCase);

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
