using System.Text.Json;

namespace CrestApps.OrchardCore.AI.Core;

public static class AIProviderConnectionExtensions
{
    public static string GetApiKey(this IDictionary<string, object> entry, bool throwException = true)
        => entry.GetStringValue("ApiKey", throwException);

    public static Uri GetEndpoint(this IDictionary<string, object> entry, bool throwException = true)
    {
        var endpoint = entry.GetStringValue("Endpoint", throwException);

        Uri uri = null;

        if (throwException)
        {
            uri = new Uri(endpoint);
        }
        else if (!string.IsNullOrEmpty(endpoint))
        {
            Uri.TryCreate(endpoint, UriKind.Absolute, out uri);
        }

        return uri;
    }

    public static string GetDefaultDeploymentName(this IDictionary<string, object> entry, bool throwException = true)
        => entry.GetStringValue("DefaultDeploymentName", throwException);

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
