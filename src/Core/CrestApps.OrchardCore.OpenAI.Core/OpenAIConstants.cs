namespace CrestApps.OrchardCore.OpenAI.Core;

public static class OpenAIConstants
{
    public const string CollectionName = "OpenAI";

    public static class Feature
    {
        public const string Area = "CrestApps.OrchardCore.OpenAI";

        public const string ChatGPT = "CrestApps.OrchardCore.OpenAI.ChatGPT";
    }

    public static class Roles
    {
        public const string System = "system";

        public const string Assistant = "assistant";

        public const string User = "user";

        public const string Function = "function";
    }

    public static class RouteNames
    {
        public const string ChatCompletionRouteName = "OpenAIChatCompletion";

        public const string ExternalChatWidget = "ExternalChatWidget";
    }

    public static class Security
    {
        public const string ExternalWidgetsCORSPolicyName = "EnableExternalWidgetsPolicy";
    }
}
