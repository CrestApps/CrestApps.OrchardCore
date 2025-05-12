namespace CrestApps.OrchardCore.OpenAI.Azure.Core;

public static class AzureOpenAIConstants
{
    public const int DefaultStrictness = 3;

    public const int DefaultTopNDocuments = 5;

    public const string ProviderName = "Azure";

    public const string StandardImplementationName = "Azure";

    public const string AISearchImplementationName = "AzureAISearch";

    public static class Feature
    {
        public const string Area = "CrestApps.OrchardCore.OpenAI.Azure";

        public const string Standard = "CrestApps.OrchardCore.OpenAI.Azure.Standard";

        public const string AISearch = "CrestApps.OrchardCore.OpenAI.Azure.AISearch";

        public const string DataSources = "CrestApps.OrchardCore.OpenAI.Azure.DataSources";
    }
}
