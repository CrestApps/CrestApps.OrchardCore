namespace CrestApps.OrchardCore.OpenAI.Core;

public static class OpenAIConstants
{
    public const string CollectionName = "OpenAI";

    public const float DefaultTemperature = 0;

    public const int DefaultMaxTokens = 800;

    public const float DefaultTopP = 1;

    public const float DefaultFrequencyPenalty = 0;

    public const float DefaultPresencePenalty = 0;

    public const int DefaultPastMessagesCount = 10;

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
