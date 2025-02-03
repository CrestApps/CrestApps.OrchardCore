using System.ClientModel;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core;

public static class OpenAIAIProviderConnectionExtensions
{
    public static string GetSubscriptionId(this AIProviderConnection entry, bool throwException = true)
        => entry.GetValueInternal("SubscriptionId", throwException);

    public static string GetClientId(this AIProviderConnection entry, bool throwException = true)
        => entry.GetValueInternal("ClientId", throwException);

    public static string GetClientSecret(this AIProviderConnection entry, bool throwException = true)
        => entry.GetValueInternal("ClientSecret", throwException);

    public static string GetTenantId(this AIProviderConnection entry, bool throwException = true)
        => entry.GetValueInternal("TenantId", throwException);

    public static string GetAccountName(this AIProviderConnection entry, bool throwException = true)
        => entry.GetValueInternal("AccountName", throwException);

    public static string GetResourceGroupName(this AIProviderConnection entry, bool throwException = true)
        => entry.GetValueInternal("ResourceGroupName", throwException);

    public static string GetApiKey(this AIProviderConnection entry, bool throwException = true)
        => entry.GetValueInternal("ApiKey", throwException);

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

    private static string GetValueInternal(this AIProviderConnection entry, string key, bool throwException)
    {
        if (entry.TryGetValue(key, out var value))
        {
            string stringValue = null;

            if (value is JsonElement jsonElement)
            {
                stringValue = jsonElement.GetString();
            }
            else if (value is string)
            {
                stringValue = value as string;
            }

            if (throwException && string.IsNullOrWhiteSpace(stringValue))
            {
                throw new InvalidOperationException($"The '{key}' does not have a value in the Azure Connection entry.");
            }

            return stringValue;
        }

        if (!throwException)
        {
            return null;
        }

        throw new InvalidOperationException($"The '{key}' does not exists in the Azure Connection entry.");
    }
}
