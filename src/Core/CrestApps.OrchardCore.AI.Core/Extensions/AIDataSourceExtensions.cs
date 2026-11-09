using CrestApps.Core.AI.Models;

namespace CrestApps.OrchardCore.AI.Core.Extensions;

/// <summary>
/// Helpers the AI data source module and its provider modules (Azure AI Search, Elasticsearch, PostgreSQL) share.
/// </summary>
public static class AIDataSourceExtensions
{
    /// <summary>
    /// The longest title <see cref="GetTitleFromContent"/> returns.
    /// </summary>
    public const int MaxContentTitleLength = 200;

    /// <summary>
    /// Gets the source type of a data source. A data source saved before sources existed has none and reads a search
    /// index profile.
    /// </summary>
    /// <param name="dataSource">The data source.</param>
    /// <returns>The source type.</returns>
    public static string GetSourceType(this AIDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);

        return string.IsNullOrWhiteSpace(dataSource.Source)
            ? AIDataSourceSourceTypes.SearchIndexProfile
            : dataSource.Source;
    }

    /// <summary>
    /// Says whether the source and field mapping of a data source can no longer change, because it already writes to a
    /// knowledge base index.
    /// </summary>
    /// <param name="dataSource">The data source.</param>
    /// <returns><see langword="true"/> when the configuration is locked.</returns>
    public static bool IsConfigurationLocked(this AIDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);

        return !string.IsNullOrWhiteSpace(dataSource.AIKnowledgeBaseIndexProfileName) &&
            !string.IsNullOrWhiteSpace(dataSource.ContentFieldName) &&
            (!string.Equals(
                dataSource.GetSourceType(),
                AIDataSourceSourceTypes.SearchIndexProfile,
                StringComparison.OrdinalIgnoreCase) ||
                !string.IsNullOrWhiteSpace(dataSource.SourceIndexProfileName));
    }

    /// <summary>
    /// Builds a title for a source document that has no title field: the first line of its content, trimmed and cut to
    /// <see cref="MaxContentTitleLength"/> characters.
    /// </summary>
    /// <param name="content">The document content.</param>
    /// <returns>The title.</returns>
    public static string GetTitleFromContent(string content)
    {
        var firstLine = content.AsSpan();
        var newlineIndex = firstLine.IndexOfAny('\r', '\n');

        if (newlineIndex > 0)
        {
            firstLine = firstLine[..newlineIndex];
        }

        if (firstLine.Length > MaxContentTitleLength)
        {
            firstLine = firstLine[..MaxContentTitleLength];
        }

        return firstLine.ToString().Trim();
    }
}
