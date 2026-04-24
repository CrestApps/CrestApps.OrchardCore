using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Infrastructure;

namespace CrestApps.OrchardCore.AI.Core;

public static class AILegacyDeploymentModelExtensions
{
    public static string GetLegacyChatDeploymentName(this AIProviderConnection connection)
        => GetLegacyString(GetProperties(connection), "ChatDeploymentName", "DeploymentName", "DefaultChatDeploymentName", "DefaultDeploymentName");

    public static void SetLegacyChatDeploymentName(this AIProviderConnection connection, string value)
        => SetLegacyValue(connection, "ChatDeploymentName", value);

    public static string GetLegacyEmbeddingDeploymentName(this AIProviderConnection connection)
        => GetLegacyString(GetProperties(connection), "EmbeddingDeploymentName", "DefaultEmbeddingDeploymentName");

    public static void SetLegacyEmbeddingDeploymentName(this AIProviderConnection connection, string value)
        => SetLegacyValue(connection, "EmbeddingDeploymentName", value);

    public static string GetLegacyImageDeploymentName(this AIProviderConnection connection)
        => GetLegacyString(GetProperties(connection), "ImagesDeploymentName", "DefaultImagesDeploymentName");

    public static void SetLegacyImageDeploymentName(this AIProviderConnection connection, string value)
        => SetLegacyValue(connection, "ImagesDeploymentName", value);

    public static string GetLegacyUtilityDeploymentName(this AIProviderConnection connection)
        => GetLegacyString(GetProperties(connection), "UtilityDeploymentName", "DefaultUtilityDeploymentName");

    public static void SetLegacyUtilityDeploymentName(this AIProviderConnection connection, string value)
        => SetLegacyValue(connection, "UtilityDeploymentName", value);

    public static string GetLegacySpeechToTextDeploymentName(this AIProviderConnection connection)
        => GetLegacyString(GetProperties(connection), "SpeechToTextDeploymentName", "DefaultSpeechToTextDeploymentName");

    public static void SetLegacySpeechToTextDeploymentName(this AIProviderConnection connection, string value)
        => SetLegacyValue(connection, "SpeechToTextDeploymentName", value);

    public static bool GetIsDefault(this AIDeployment deployment)
        => GetProperties(deployment).GetBooleanOrFalseValue("IsDefault");

    public static void SetIsDefault(this AIDeployment deployment, bool isDefault)
        => SetLegacyValue(deployment, "IsDefault", isDefault);

    public static string GetConnectionDisplayName(this AIDeployment deployment)
        => deployment.ConnectionName;

    private static IDictionary<string, object> GetProperties(ExtensibleEntity entity)
    {
        entity.Properties ??= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        return entity.Properties;
    }

    private static void SetLegacyValue(ExtensibleEntity entity, string key, object value)
    {
        var properties = GetProperties(entity);

        if (value is string stringValue && string.IsNullOrWhiteSpace(stringValue))
        {
            properties.Remove(key);
            return;
        }

        properties[key] = value;
    }

    private static string GetLegacyString(IDictionary<string, object> properties, params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = properties.GetStringValue(key, false);

            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}
