namespace CrestApps.OrchardCore.AI.Core;

public static class DataSourceConstants
{
    public static readonly string IndexingTaskType = "DataSourceIndex";

    public static class ColumnNames
    {
        public const string ReferenceId = "referenceId";

        public const string DataSourceId = "dataSourceId";

        public const string ChunkId = "chunkId";

        public const string ChunkIndex = "chunkIndex";

        public const string Title = "title";

        public const string Content = "content";

        public const string Embedding = "embedding";

        public const string Timestamp = "timestamp";

        public const string ReferenceType = "referenceType";

        public const string Filters = "filters";
    }
}
