namespace CrestApps.OrchardCore.AI.Core;

public static class AIConstants
{
    public const string TitleGeneratorSystemMessage =
    """
    - Generate a short topic title about the user prompt.
    - Response using title case.
    - Response must be under 255 characters in length.
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
        public const string OrchardCoreAIAgentRecipes = OrchardCoreAIAgent + ".Recipes";
        public const string OrchardCoreAIAgentTenants = OrchardCoreAIAgent + ".Tenants";
        public const string OrchardCoreAIAgentContents = OrchardCoreAIAgent + ".Contents";
        public const string OrchardCoreAIAgentContentDefinitionRecipesTools = OrchardCoreAIAgent + ".ContentDefinition.Recipes.Tools";
        public const string OrchardCoreAIAgentContentDefinitions = OrchardCoreAIAgent + ".ContentDefinitions";
        public const string OrchardCoreAIAgentFeatures = OrchardCoreAIAgent + ".Features";
        public const string OrchardCoreAIAgentNotifications = OrchardCoreAIAgent + ".Notifications";
        public const string OrchardCoreAIAgentEmail = OrchardCoreAIAgent + ".Email";
        public const string OrchardCoreAIAgentSms = OrchardCoreAIAgent + ".Sms";
        public const string OrchardCoreAIAgentUsers = OrchardCoreAIAgent + ".Users";
        public const string OrchardCoreAIAgentRoles = OrchardCoreAIAgent + ".Roles";
        public const string OrchardCoreAIAgentWorkflows = OrchardCoreAIAgent + ".Workflows";


        public const string ChatCore = "CrestApps.OrchardCore.AI.Chat.Core";

        public const string Chat = "CrestApps.OrchardCore.AI.Chat";

        public const string DataSources = "CrestApps.OrchardCore.AI.DataSources";

        public const string ChatApi = "CrestApps.OrchardCore.AI.Chat.Api";
    }

    public static class RouteNames
    {
        public const string AICompletionRoute = "AIChatCompletion";

        public const string AIUtilityCompletionRouteName = "AIUtilityCompletion";

        public const string AIChatSessionRouteName = "AIChatSession";

        public const string GetDeploymentsByConnectionRouteName = "GetDeploymentsByConnection";
    }
}
