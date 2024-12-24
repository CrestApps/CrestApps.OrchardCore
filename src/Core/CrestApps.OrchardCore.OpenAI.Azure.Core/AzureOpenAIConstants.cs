namespace CrestApps.OrchardCore.OpenAI.Azure.Core;

public static class AzureOpenAIConstants
{
    public const float DefaultTemperature = 0;

    public const int DefaultTokenLength = 800;

    public const float DefaultTopP = 1;

    public const float DefaultFrequencyPenalty = 0;

    public const float DefaultPresencePenalty = 0;

    public const int DefaultPastMessagesCount = 10;

    public const int DefaultStrictness = 3;

    public const int DefaultTopNDocuments = 5;

    public const string ChatSearchProviderName = "ChatAI";

    public static class Roles
    {
        public const string System = "system";

        public const string Assistant = "assistant";

        public const string User = "user";
    }
}
