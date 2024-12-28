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

    public const string AzureDeploymentSourceName = "Azure";

    public const string HttpClientName = "AzureOpenAIClient";

    public static class Feature
    {
        public const string Area = "CrestApps.OrchardCore.OpenAI.Azure";

        public const string Deployments = "CrestApps.OrchardCore.OpenAI.Azure.Deployments";

        public const string Standard = "CrestApps.OrchardCore.OpenAI.Azure.Standard";

        public const string AISearch = "CrestApps.OrchardCore.OpenAI.Azure.AISearch";
    }
}
