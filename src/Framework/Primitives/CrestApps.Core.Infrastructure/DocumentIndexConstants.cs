using CrestApps.Core.Infrastructure.Indexing;

namespace CrestApps.Core.Infrastructure;

/// <summary>
/// Constants for document chunk index field names used by
/// <see cref="IVectorSearchService"/> and <see cref="ISearchDocumentManager"/> implementations.
/// </summary>
public static class DocumentIndexConstants
{
    public static class ColumnNames
    {
        public const string ChunkId = "chunkId";

        public const string Content = "content";

        public const string DocumentId = "documentId";

        public const string FileName = "fileName";

        public const string ReferenceId = "referenceId";

        public const string ReferenceType = "referenceType";

        public const string Embedding = "embedding";

        public const string ChunkIndex = "chunkIndex";
    }

    public static class MemoryColumnNames
    {
        public const string MemoryId = "memoryId";

        public const string UserId = "userId";

        public const string Name = "name";

        public const string Description = "description";

        public const string Content = "content";

        public const string Embedding = "embedding";

        public const string UpdatedUtc = "updatedUtc";
    }
}
