using System.Reflection;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Infrastructure.Indexing.Models;

namespace CrestApps.OrchardCore.AI.Core;

public static class EmbeddingDeploymentNameAccessor
{
    private const string EmbeddingDeploymentNamePropertyName = "EmbeddingDeploymentName";
    private const string EmbeddingDeploymentIdPropertyName = "EmbeddingDeploymentId";

    public static string GetEmbeddingDeploymentName(this DataSourceIndexProfileMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        return GetStringPropertyValue(metadata, EmbeddingDeploymentNamePropertyName) ??
            GetStringPropertyValue(metadata, EmbeddingDeploymentIdPropertyName);
    }

    public static void SetEmbeddingDeploymentName(this DataSourceIndexProfileMetadata metadata, string value)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        if (TrySetStringPropertyValue(metadata, EmbeddingDeploymentNamePropertyName, value))
        {
            return;
        }

        TrySetStringPropertyValue(metadata, EmbeddingDeploymentIdPropertyName, value);
    }

    public static string GetEmbeddingDeploymentName(this SearchIndexProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return GetStringPropertyValue(profile, EmbeddingDeploymentNamePropertyName) ??
            GetStringPropertyValue(profile, EmbeddingDeploymentIdPropertyName);
    }

    public static void SetEmbeddingDeploymentName(this SearchIndexProfile profile, string value)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (TrySetStringPropertyValue(profile, EmbeddingDeploymentNamePropertyName, value))
        {
            return;
        }

        TrySetStringPropertyValue(profile, EmbeddingDeploymentIdPropertyName, value);
    }

    private static string GetStringPropertyValue(object instance, string propertyName)
        => instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)?.GetValue(instance) as string;

    private static bool TrySetStringPropertyValue(object instance, string propertyName, string value)
    {
        var property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);

        if (property?.CanWrite != true || property.PropertyType != typeof(string))
        {
            return false;
        }

        property.SetValue(instance, value);

        return true;
    }
}
