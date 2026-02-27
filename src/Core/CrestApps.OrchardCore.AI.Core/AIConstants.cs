using OrchardCore.Indexing.Core;

namespace CrestApps.OrchardCore.AI.Core;

public static class AIConstants
{
    public const string DefaultBlankMessage = "AI drew blank and no message was generated!";

    public const string DefaultBlankSessionTitle = "Untitled";

    public const string CollectionName = "AI";

    public const string ConnectionProtectorName = "AIProviderConnection";

    public const string AISettingsGroupId = "ai-settings";

    public static class Feature
    {
        public const string Area = "CrestApps.OrchardCore.AI";

        public const string ConnectionManagement = "CrestApps.OrchardCore.AI.ConnectionManagement";

        public const string Deployments = "CrestApps.OrchardCore.AI.Deployments";

        public const string OrchardCoreAIAgent = "CrestApps.OrchardCore.AI.Agent";

        public const string ChatCore = "CrestApps.OrchardCore.AI.Chat.Core";

        public const string Chat = "CrestApps.OrchardCore.AI.Chat";

        public const string DataSources = "CrestApps.OrchardCore.AI.DataSources";

        public const string DataSourceElasticsearch = "CrestApps.OrchardCore.AI.DataSources.Elasticsearch";

        public const string DataSourceAzureAI = "CrestApps.OrchardCore.AI.DataSources.AzureAI";

        public const string DataSourceMongoDB = "CrestApps.OrchardCore.AI.DataSources.MongoDB";

        public const string ChatApi = "CrestApps.OrchardCore.AI.Chat.Api";

        public const string ProfileDocuments = "CrestApps.OrchardCore.AI.Documents.Profiles";

        public const string ChatAdminWidget = "CrestApps.OrchardCore.AI.Chat.AdminWidget";

        public const string ChatSessionDocuments = "CrestApps.OrchardCore.AI.Documents.ChatSessions";

        public const string ChatAnalytics = "CrestApps.OrchardCore.AI.Chat.Analytics";
    }

    public static readonly string AIDocumentsIndexingTaskType = "AIDocuments";

    public static class DocumentReferenceTypes
    {
        public const string Profile = "profile";

        public const string ChatInteraction = "chat-interaction";

        public const string ChatSession = "chat-session";
    }

    public static class DataSourceReferenceTypes
    {
        /// <summary>
        /// Reference type for content items indexed by OrchardCore.
        /// Matches <c>IndexingConstants.ContentsIndexSource</c>.
        /// </summary>
        public const string Content = IndexingConstants.ContentsIndexSource;

        /// <summary>
        /// Reference type for uploaded documents in chat interactions or profiles.
        /// </summary>
        public const string Document = "Document";
    }

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

    public static class RouteNames
    {
        public const string AICompletionRoute = "AIChatCompletion";

        public const string AIUtilityCompletionRouteName = "AIUtilityCompletion";

        public const string AIChatSessionRouteName = "AIChatSession";

        public const string GetDeploymentsByConnectionRouteName = "GetDeploymentsByConnection";

        public const string GetConnectionsByProviderRouteName = "GetConnectionsByProvider";

        public const string ChatInteractionUploadDocument = "ChatInteractionUploadDocument";

        public const string ChatInteractionRemoveDocument = "ChatInteractionRemoveDocument";

        public const string ChatSessionUploadDocument = "ChatSessionUploadDocument";

        public const string ChatSessionRemoveDocument = "ChatSessionRemoveDocument";
    }
}
