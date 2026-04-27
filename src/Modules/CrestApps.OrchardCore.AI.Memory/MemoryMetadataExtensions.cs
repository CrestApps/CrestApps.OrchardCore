using System.Text.Json;
using CrestApps.Core.AI.Models;

namespace CrestApps.Core.AI.Memory;

/// <summary>
/// Provides extension methods for memory metadata.
/// </summary>
public static class MemoryMetadataExtensions
{
    public const string LegacyAIProfileSettingsKey = "AIProfileMemorySettings";
    public const string LegacyMvcMemorySettingsKey = "MvcMemorySettings";
    public const string LegacyChatInteractionSettingsKey = "ChatInteractionMemorySettings";
    private const string MemoryMetadataKey = nameof(MemoryMetadata);

    /// <summary>
    /// Retrieves the memory metadata.
    /// </summary>
    /// <param name="profile">The profile.</param>
    public static MemoryMetadata GetMemoryMetadata(this AIProfile profile)
        => profile.Get<MemoryMetadata>(MemoryMetadataKey) ?? new MemoryMetadata();

    /// <summary>
    /// Retrieves the memory metadata.
    /// </summary>
    /// <param name="template">The template.</param>
    public static MemoryMetadata GetMemoryMetadata(this AIProfileTemplate template)
        => template.Get<MemoryMetadata>(MemoryMetadataKey) ?? new MemoryMetadata();

    /// <summary>
    /// Performs the alter memory metadata operation.
    /// </summary>
    /// <param name="profile">The profile.</param>
    /// <param name="configure">The configure.</param>
    public static void AlterMemoryMetadata(
        this AIProfile profile,
        Action<MemoryMetadata> configure)
    {
        profile.Alter(configure);
    }

    /// <summary>
    /// Performs the with memory metadata operation.
    /// </summary>
    /// <param name="template">The template.</param>
    /// <param name="metadata">The metadata.</param>
    public static AIProfileTemplate WithMemoryMetadata(
        this AIProfileTemplate template,
        MemoryMetadata metadata)
    {
        template.Put(metadata);

        return template;
    }

    /// <summary>
    /// Performs the try deserialize operation.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="metadata">The metadata.</param>
    public static bool TryDeserialize(
        object value,
        out MemoryMetadata metadata)
    {
        metadata = default;

        if (value is null)
        {
            return false;
        }

        if (value is MemoryMetadata typedMetadata)
        {
            metadata = typedMetadata;
            return true;
        }

        try
        {
            metadata = value switch
            {
                JsonElement element => element.Deserialize<MemoryMetadata>(),
                string json when !string.IsNullOrWhiteSpace(json) => JsonSerializer.Deserialize<MemoryMetadata>(json),
                _ => JsonSerializer.Deserialize<MemoryMetadata>(JsonSerializer.Serialize(value)),
            };

            return metadata is not null;
        }
        catch (JsonException)
        {
            metadata = default;
            return false;
        }
    }
}
