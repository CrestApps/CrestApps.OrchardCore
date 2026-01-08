namespace CrestApps.OrchardCore.AI.Core;

public static class AIConstants
{
    public const string TitleGeneratorSystemMessage =
    """
    - Create a concise title that reflects the main topic of the user's prompt.
    - Use Title Case.
    - Do not use markdown, symbols, or decorative formatting.
    - Keep the title under 255 characters.
    """;

    public const string DefaultBlankMessage = "AI drew blank and no message was generated!";

    public const string CollectionName = "AI";

    public const string ConnectionProtectorName = "AIProviderConnection";

    public static class SystemMessages
    {
        public const string UseMarkdownSyntax = "- Provide a response using Markdown syntax.";
    }

    public static class Feature
    {
        public const string Area = "CrestApps.OrchardCore.AI";

        public const string ConnectionManagement = "CrestApps.OrchardCore.AI.ConnectionManagement";

        public const string Deployments = "CrestApps.OrchardCore.AI.Deployments";

        public const string Tools = "CrestApps.OrchardCore.AI.Tools";

        public const string OrchardCoreAIAgent = "CrestApps.OrchardCore.AI.Agent";

        public const string ChatCore = "CrestApps.OrchardCore.AI.Chat.Core";

        public const string Chat = "CrestApps.OrchardCore.AI.Chat";

        public const string DataSources = "CrestApps.OrchardCore.AI.DataSources";

        public const string ChatApi = "CrestApps.OrchardCore.AI.Chat.Api";

        public const string ChatInteractions = "CrestApps.OrchardCore.AI.Chat.Interactions";

        public const string ChatDocuments = "CrestApps.OrchardCore.AI.Chat.Interactions.Documents";
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
    }
}
