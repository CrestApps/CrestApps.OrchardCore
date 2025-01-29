namespace CrestApps.OrchardCore.OpenAI.Core;

public static class OpenAIConstants
{
    public const string CollectionName = "OpenAI";

    public const string DefaultBlankMessage = "AI drew blank and no message was generated!";

    public static class SystemMessages
    {
        public const string UseMarkdownSyntax = "- Provide a response using Markdown syntax.";
    }

    public static class Feature
    {
        public const string Area = "CrestApps.OrchardCore.OpenAI";

        public const string ChatGPT = "CrestApps.OrchardCore.OpenAI.ChatGPT";
    }

    public static class RouteNames
    {
        public const string ChatCompletionRouteName = "OpenAIChatCompletion";

        public const string ChatUtilityCompletionRouteName = "OpenAIChatUtilityCompletion";

        public const string ChatSessionRouteName = "OpenAIChatSession";
    }
}
