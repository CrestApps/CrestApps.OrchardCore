using System.ClientModel;
using Azure.Core;
using Azure.Identity;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core;

public static class OpenAIAIProviderConnectionExtensions
{
    public static string GetSubscriptionId(this AIProviderConnectionEntry entry, bool throwException = true)
        => entry.GetStringValue("SubscriptionId", throwException);

    public static string GetClientId(this AIProviderConnectionEntry entry, bool throwException = true)
        => entry.GetStringValue("ClientId", throwException);

    public static string GetClientSecret(this AIProviderConnectionEntry entry, bool throwException = true)
        => entry.GetStringValue("ClientSecret", throwException);

    public static string GetTenantId(this AIProviderConnectionEntry entry, bool throwException = true)
        => entry.GetStringValue("TenantId", throwException);

    public static string GetAccountName(this AIProviderConnectionEntry entry, bool throwException = true)
        => entry.GetStringValue("AccountName", throwException);

    public static string GetResourceGroupName(this AIProviderConnectionEntry entry, bool throwException = true)
        => entry.GetStringValue("ResourceGroupName", throwException);

    public static TokenCredential GetCredential(this AIProviderConnectionEntry entry)
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

    public static ApiKeyCredential GetApiKeyCredential(this AIProviderConnectionEntry entry)
        => new(entry.GetApiKey());
}
