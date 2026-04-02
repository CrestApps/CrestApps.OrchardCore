using CrestApps.Infrastructure.Indexing;

namespace CrestApps.Mvc.Web.Services;

public static class IndexProfileTypeRules
{
    public static readonly string[] EmbeddingTypes =
    [
        IndexProfileTypes.AIDocuments,
        IndexProfileTypes.AIMemory,
        IndexProfileTypes.DataSource,
    ];

    public static bool RequiresEmbedding(string type)
        => EmbeddingTypes.Contains(type, StringComparer.OrdinalIgnoreCase);

    public static bool SupportsEmbeddingSelection(string type)
        => !string.Equals(type, IndexProfileTypes.Articles, StringComparison.OrdinalIgnoreCase);
}
