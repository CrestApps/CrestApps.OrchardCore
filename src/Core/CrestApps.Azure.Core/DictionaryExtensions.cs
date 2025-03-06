using System.Text.Json;
using CrestApps.OrchardCore.Azure.Core.Models;

namespace CrestApps.Azure.Core;

public static class DictionaryExtensions
{
    public static AzureAuthenticationType GetAzureAuthenticationType(this IDictionary<string, object> entry)
    {
        var authenticationTypeString = entry.GetStringValue("AuthenticationType");

        if (string.IsNullOrEmpty(authenticationTypeString) ||
            !Enum.TryParse<AzureAuthenticationType>(authenticationTypeString, true, out var authenticationType))
        {
            authenticationType = AzureAuthenticationType.Default;
        }

        return authenticationType;
    }

    public static string GetStringValue(this IDictionary<string, object> entry, string key, bool throwException = false)
    {
        if (entry.TryGetValue(key, out var value))
        {
            string stringValue;

            if (value is JsonElement jsonElement)
            {
                stringValue = jsonElement.GetString();
            }
            else if (value is string)
            {
                stringValue = value as string;
            }
            else
            {
                stringValue = value?.ToString();
            }

            if (throwException && string.IsNullOrWhiteSpace(stringValue))
            {
                throw new InvalidOperationException($"The '{key}' does not have a value in the dictionary.");
            }

            return stringValue;
        }

        if (!throwException)
        {
            return null;
        }

        throw new InvalidOperationException($"The '{key}' does not exists in the dictionary.");
    }
}
