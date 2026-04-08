namespace CrestApps.Core.AI.Memory;

public static class MemoryConstants
{
    public const string CollectionName = "AIMemory";
    public const string IndexingTaskType = "AIMemory";

    public static class Feature
    {
        public const string Memory = "CrestApps.OrchardCore.AI.Memory";
    }

    public static class CompletionContextKeys
    {
        public const string UserId = "AIMemoryUserId";
    }

    public static class ToolPurposes
    {
        public const string UserMemory = "user_memory";
    }

    public static class ColumnNames
    {
        public const string MemoryId = "memoryId";
        public const string UserId = "userId";
        public const string Name = "name";
        public const string Description = "description";
        public const string Content = "content";
        public const string UpdatedUtc = "updatedUtc";
        public const string Embedding = "embedding";
    }

    public static class TemplateIds
    {
        public const string MemoryAvailability = "memory-availability";
        public const string MemoryContextHeader = "memory-context-header";
    }
}
