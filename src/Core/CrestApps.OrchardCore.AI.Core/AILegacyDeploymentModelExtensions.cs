using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Infrastructure;

namespace CrestApps.OrchardCore.AI.Core;

/// <summary>
/// Provides extension methods for AI legacy deployment model.
/// </summary>
public static class AILegacyDeploymentModelExtensions
{
    /// <summary>
    /// Retrieves the legacy chat deployment name.
    /// </summary>
    /// <param name="connection">The connection.</param>
    public static string GetLegacyChatDeploymentName(this AIProviderConnection connection)
        => GetLegacyString(GetProperties(connection), "ChatDeploymentName", "DeploymentName", "DefaultChatDeploymentName", "DefaultDeploymentName");

    /// <summary>
    /// Sets the legacy chat deployment name.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <param name="value">The value.</param>
    public static void SetLegacyChatDeploymentName(this AIProviderConnection connection, string value)
        => SetLegacyValue(connection, "ChatDeploymentName", value);

    /// <summary>
    /// Retrieves the legacy embedding deployment name.
    /// </summary>
    /// <param name="connection">The connection.</param>
    public static string GetLegacyEmbeddingDeploymentName(this AIProviderConnection connection)
        => GetLegacyString(GetProperties(connection), "EmbeddingDeploymentName", "DefaultEmbeddingDeploymentName");

    /// <summary>
    /// Sets the legacy embedding deployment name.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <param name="value">The value.</param>
    public static void SetLegacyEmbeddingDeploymentName(this AIProviderConnection connection, string value)
        => SetLegacyValue(connection, "EmbeddingDeploymentName", value);

    /// <summary>
    /// Retrieves the legacy image deployment name.
    /// </summary>
    /// <param name="connection">The connection.</param>
    public static string GetLegacyImageDeploymentName(this AIProviderConnection connection)
        => GetLegacyString(GetProperties(connection), "ImagesDeploymentName", "DefaultImagesDeploymentName");

    /// <summary>
    /// Sets the legacy image deployment name.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <param name="value">The value.</param>
    public static void SetLegacyImageDeploymentName(this AIProviderConnection connection, string value)
        => SetLegacyValue(connection, "ImagesDeploymentName", value);

    /// <summary>
    /// Retrieves the legacy utility deployment name.
    /// </summary>
    /// <param name="connection">The connection.</param>
    public static string GetLegacyUtilityDeploymentName(this AIProviderConnection connection)
        => GetLegacyString(GetProperties(connection), "UtilityDeploymentName", "DefaultUtilityDeploymentName");

    /// <summary>
    /// Sets the legacy utility deployment name.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <param name="value">The value.</param>
    public static void SetLegacyUtilityDeploymentName(this AIProviderConnection connection, string value)
        => SetLegacyValue(connection, "UtilityDeploymentName", value);

    /// <summary>
    /// Retrieves the legacy speech to text deployment name.
    /// </summary>
    /// <param name="connection">The connection.</param>
    public static string GetLegacySpeechToTextDeploymentName(this AIProviderConnection connection)
        => GetLegacyString(GetProperties(connection), "SpeechToTextDeploymentName", "DefaultSpeechToTextDeploymentName");

    /// <summary>
    /// Sets the legacy speech to text deployment name.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <param name="value">The value.</param>
    public static void SetLegacySpeechToTextDeploymentName(this AIProviderConnection connection, string value)
        => SetLegacyValue(connection, "SpeechToTextDeploymentName", value);

    /// <summary>
    /// Retrieves the is default.
    /// </summary>
    /// <param name="deployment">The deployment.</param>
    public static bool GetIsDefault(this AIDeployment deployment)
        => GetProperties(deployment).GetBooleanOrFalseValue("IsDefault");

    /// <summary>
    /// Sets the is default.
    /// </summary>
    /// <param name="deployment">The deployment.</param>
    /// <param name="isDefault">The is default.</param>
    public static void SetIsDefault(this AIDeployment deployment, bool isDefault)
        => SetLegacyValue(deployment, "IsDefault", isDefault);

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
