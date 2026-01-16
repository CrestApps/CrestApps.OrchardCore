namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core;

public static class ChatInteractionsConstants
{
    public static readonly string IndexingTaskType = "ChatInteractionDocuments";

    public static class ColumnNames
    {
        public const string Chunks = "chunks";

        public const string Text = "text";

        public const string DocumentId = "documentId";

        public const string FileName = "fileName";

        public const string InteractionId = "interactionId";

        public const string ChunksEmbedding = "chunks.embedding";

        public const string ChunksText = "chunks.text";

        public static class ChunksColumnNames
        {
            public const string Text = "text";

            public const string Embedding = "embedding";

            public const string Index = "index";
        }
    }

    public static class Feature
    {
        public const string ChatInteractions = "CrestApps.OrchardCore.AI.Chat.Interactions";

        public const string ChatDocuments = "CrestApps.OrchardCore.AI.Chat.Interactions.Documents";
    }
}
