namespace CrestApps.OrchardCore.OpenAI.Core;

public static class OpenAIConstants
{
    public const string CollectionName = "OpenAI";

    public static class Feature
    {
        public const string Area = "CrestApps.OrchardCore.OpenAI";
    }

    public static class Roles
    {
        public const string System = "system";

        public const string Assistant = "assistant";

        public const string User = "user";
    }

    public static class RouteNames
    {
        public const string ChatRouteName = "OpenAI-Chat";

        public const string ExternalChatWidget = "ExternalChatWidget";
    }

    public static class Security
    {
        public const string ExternalWidgetsCORSPolicyName = "EnableExternalWidgetsPolicy";
    }
}