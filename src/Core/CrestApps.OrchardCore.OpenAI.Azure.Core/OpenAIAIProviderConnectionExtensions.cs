using System.ClientModel;
using Azure.Core;
using Azure.Identity;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core;

public static class OpenAIAIProviderConnectionExtensions
{
    public static string GetSubscriptionId(this AIProviderConnection entry, bool throwException = true)
        => entry.GetStringValue("SubscriptionId", throwException);

    public static string GetClientId(this AIProviderConnection entry, bool throwException = true)
        => entry.GetStringValue("ClientId", throwException);

    public static string GetClientSecret(this AIProviderConnection entry, bool throwException = true)
        => entry.GetStringValue("ClientSecret", throwException);

    public static string GetTenantId(this AIProviderConnection entry, bool throwException = true)
        => entry.GetStringValue("TenantId", throwException);

    public static string GetAccountName(this AIProviderConnection entry, bool throwException = true)
        => entry.GetStringValue("AccountName", throwException);

    public static string GetResourceGroupName(this AIProviderConnection entry, bool throwException = true)
        => entry.GetStringValue("ResourceGroupName", throwException);

    public static string GetApiKey(this AIProviderConnection entry, bool throwException = true)
        => entry.GetStringValue("ApiKey", throwException);

    public static TokenCredential GetCredential(this AIProviderConnection entry)
    {
        var tenantId = entry.GetTenantId(false);
        var clientId = entry.GetClientId(false);
        var clientSecret = entry.GetClientSecret(false);

        if (!string.IsNullOrEmpty(tenantId) &&
            !string.IsNullOrEmpty(clientId) &&
            !string.IsNullOrEmpty(clientSecret))
        {
            return new ClientSecretCredential(tenantId, clientId, clientSecret);
        }

        return new DefaultAzureCredential();
    }

    public static ApiKeyCredential GetApiKeyCredential(this AIProviderConnection entry)
        => new(entry.GetApiKey());
}
