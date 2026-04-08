namespace CrestApps.Core.AI.Services;

public static class AIProviderNameNormalizer
{
    private const string _azureOpenAIClientName = "Azure";

    public static string Normalize(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            return providerName;
        }

        return string.Equals(providerName, "AzureOpenAI", StringComparison.OrdinalIgnoreCase)
            ? _azureOpenAIClientName
            : providerName;
    }
}
