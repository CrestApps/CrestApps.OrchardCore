using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.DataProtection;

namespace CrestApps.OrchardCore.AI.Core.Services;

internal static class AIDeploymentConnectionEntryFactory
{
    public static AIProviderConnectionEntry Create(AIDeployment deployment, IDataProtectionProvider dataProtectionProvider)
    {
        var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        if (deployment.Properties != null)
        {
            foreach (var property in deployment.Properties)
            {
                values[property.Key] = ConvertJsonNode(property.Value);
            }
        }

        UnprotectApiKeys(values, dataProtectionProvider);

        return new AIProviderConnectionEntry(values);
    }

    private static object ConvertJsonNode(JsonNode node)
    {
        return node switch
        {
            JsonObject jsonObject => jsonObject.ToDictionary(
                property => property.Key,
                property => ConvertJsonNode(property.Value),
                StringComparer.OrdinalIgnoreCase),
            JsonArray jsonArray => jsonArray.Select(ConvertJsonNode).ToList(),
            JsonValue jsonValue when jsonValue.TryGetValue<string>(out var s) => s,
            JsonValue jsonValue when jsonValue.TryGetValue<bool>(out var b) => b,
            JsonValue jsonValue when jsonValue.TryGetValue<int>(out var i) => i,
            JsonValue jsonValue when jsonValue.TryGetValue<long>(out var l) => l,
            JsonValue jsonValue when jsonValue.TryGetValue<double>(out var d) => d,
            _ => node?.ToString(),
        };
    }

    private static void UnprotectApiKeys(IDictionary<string, object> values, IDataProtectionProvider dataProtectionProvider)
    {
        foreach (var (key, value) in values.ToList())
        {
            switch (value)
            {
                case IDictionary<string, object> nestedDictionary:
                    UnprotectApiKeys(nestedDictionary, dataProtectionProvider);
                    break;

                case List<object> items:
                    UnprotectApiKeys(items, dataProtectionProvider);
                    break;

                case string encryptedKey when
                    string.Equals(key, "ApiKey", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(encryptedKey):
                    {
                        var protector = dataProtectionProvider.CreateProtector(AIConstants.ConnectionProtectorName);
                        values[key] = protector.Unprotect(encryptedKey);
                        break;
                    }
            }
        }
    }

    private static void UnprotectApiKeys(List<object> values, IDataProtectionProvider dataProtectionProvider)
    {
        foreach (var value in values)
        {
            switch (value)
            {
                case IDictionary<string, object> nestedDictionary:
                    UnprotectApiKeys(nestedDictionary, dataProtectionProvider);
                    break;

                case List<object> nestedList:
                    UnprotectApiKeys(nestedList, dataProtectionProvider);
                    break;
            }
        }
    }
}
