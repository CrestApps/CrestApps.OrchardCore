using System.ClientModel;
using Azure.Core;
using Azure.Identity;
using CrestApps.OrchardCore.OpenAI.Models;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core;

public static class OpenAIConnectionEntryExtensions
{
    public static string GetSubscriptionId(this OpenAIConnectionEntry entry, bool throwException = true)
        => entry.GetValueInternal("SubscriptionId", throwException);

    public static string GetClientId(this OpenAIConnectionEntry entry, bool throwException = true)
        => entry.GetValueInternal("ClientId", throwException);

    public static string GetClientSecret(this OpenAIConnectionEntry entry, bool throwException = true)
        => entry.GetValueInternal("ClientSecret", throwException);

    public static string GetTenantId(this OpenAIConnectionEntry entry, bool throwException = true)
        => entry.GetValueInternal("TenantId", throwException);

    public static string GetAccountName(this OpenAIConnectionEntry entry, bool throwException = true)
        => entry.GetValueInternal("AccountName", throwException);

    public static string GetResourceGroupName(this OpenAIConnectionEntry entry, bool throwException = true)
        => entry.GetValueInternal("ResourceGroupName", throwException);

    public static string GetApiKey(this OpenAIConnectionEntry entry, bool throwException = true)
        => entry.GetValueInternal("ApiKey", throwException);

    public static TokenCredential GetCredential(this OpenAIConnectionEntry entry)
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

    public static ApiKeyCredential GetApiKeyCredential(this OpenAIConnectionEntry entry)
        => new(entry.GetApiKey());

    private static string GetValueInternal(this OpenAIConnectionEntry entry, string key, bool throwException)
    {
        if (entry.Data.TryGetPropertyValue(key, out var value))
        {
            var stringValue = value.GetValue<string>();

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
