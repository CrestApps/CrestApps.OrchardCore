namespace CrestApps.OrchardCore.AI.Core;

public static class DataSourceConstants
{
    public static readonly string IndexingTaskType = "DataSourceIndex";

    public static class ColumnNames
    {
        public const string ReferenceId = "referenceId";

        public const string DataSourceId = "dataSourceId";

        public const string Title = "title";

        public const string Text = "text";

        public const string Timestamp = "timestamp";

        public const string Chunks = "chunks";

        public const string ChunksEmbedding = "chunks.embedding";

        public const string ChunksText = "chunks.text";

        public static class ChunksColumnNames
        {
            public const string Text = "text";

            public const string Embedding = "embedding";

            public const string Index = "index";
        }
    }
}
