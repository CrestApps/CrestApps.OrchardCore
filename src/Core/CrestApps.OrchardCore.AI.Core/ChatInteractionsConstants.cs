namespace CrestApps.OrchardCore.AI.Core;

public static class ChatInteractionsConstants
{
    public static readonly string IndexingTaskType = "ChatInteractionsDocumentIndex";

    public static class ColumnNames
    {
        public const string Chunks = "chunks";

        public const string Text = "text";

        public const string DocumentId = "documentId";

        public const string FileName = "fileName";

        public const string InteractionId = "interactionId";

        public const string ChunksEmbedding = "chunks.embedding";

        public static class ChunksColumnNames
        {
            public const string Text = "text";

            public const string Embedding = "embedding";

            public const string Index = "index";
        }
    }

    public static class Feature
    {
        public const string ModuleName = "CrestApps.OrchardCore.AI.Chat.Interactions";
    }
}
