namespace CrestApps.OrchardCore.AI.Core;

public static class AIConstants
{
    public const string CollectionName = "AI";

    public const string DefaultBlankMessage = "AI drew blank and no message was generated!";

    public static class SystemMessages
    {
        public const string UseMarkdownSyntax = "- Provide a response using Markdown syntax.";
    }

    public static class Feature
    {
        public const string Area = "CrestApps.OrchardCore.AI";

        public const string Chat = "CrestApps.OrchardCore.AI.Chat";
    }

    public static class RouteNames
    {
        public const string ChatCompletionRouteName = "AIChatCompletion";

        public const string ChatUtilityCompletionRouteName = "AIChatUtilityCompletion";

        public const string ChatSessionRouteName = "AIChatSession";
    }
}
