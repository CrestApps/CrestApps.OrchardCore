namespace CrestApps.OrchardCore.OpenAI.Azure.Core;

public static class AzureOpenAIConstants
{
    public const int DefaultStrictness = 3;

    public const int DefaultTopNDocuments = 3;

    public const string ProviderName = "Azure";

    public const string MongoDataProtectionPurpose = "MongoDBDataProtection";

    public static class Feature
    {
        public const string Area = "CrestApps.OrchardCore.OpenAI.Azure";

        public const string Standard = "CrestApps.OrchardCore.OpenAI.Azure.Standard";

        public const string AISearch = "CrestApps.OrchardCore.OpenAI.Azure.AISearch";

        public const string Elasticsearch = "CrestApps.OrchardCore.OpenAI.Azure.Elasticsearch";

        public const string MongoDB = "CrestApps.OrchardCore.OpenAI.Azure.MongoDB";
    }

    public static class DataSourceTypes
    {
        public const string AzureAISearch = "azure_search";

        public const string Elasticsearch = "elasticsearch";

        public const string MongoDB = "mongo_db";
    }
}
