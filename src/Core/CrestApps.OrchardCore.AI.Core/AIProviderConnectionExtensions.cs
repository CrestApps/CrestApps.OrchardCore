using System.Text.Json;
using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI.Core;

public static class AIProviderConnectionExtensions
{
    public static string GetDefaultDeploymentName(this AIProviderConnection entry, bool throwException = true)
        => entry.GetStringValue("GetDefaultDeploymentName", throwException);

    public static string GetStringValue(this AIProviderConnection entry, string key, bool throwException = true)
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
