using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core;

namespace CrestApps.Core.AI.Models;

public static class MemoryMetadataExtensions
{
    public const string LegacyAIProfileSettingsKey = "AIProfileMemorySettings";
    public const string LegacyChatInteractionSettingsKey = "ChatInteractionMemorySettings";
    public const string LegacyMvcMemorySettingsKey = "MemorySettings";

    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static MemoryMetadata GetMemoryMetadata(this AIProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (profile.Has<MemoryMetadata>())
        {
            return profile.As<MemoryMetadata>();
        }

        if (TryDeserialize(profile.Settings?[LegacyAIProfileSettingsKey], out MemoryMetadata metadata) ||
            TryDeserialize(profile.Settings?[LegacyMvcMemorySettingsKey], out metadata))
        {
            return metadata;
        }

        return new MemoryMetadata();
    }

    public static AIProfile AlterMemoryMetadata(this AIProfile profile, Action<MemoryMetadata> alter)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(alter);

        profile.Alter<MemoryMetadata>(alter);
        profile.Settings?.Remove(LegacyAIProfileSettingsKey);
        profile.Settings?.Remove(LegacyMvcMemorySettingsKey);

        return profile;
    }

    public static MemoryMetadata GetMemoryMetadata(this AIProfileTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);

        if (template.Has<MemoryMetadata>())
        {
            return template.As<MemoryMetadata>();
        }

        if (TryDeserialize(GetPropertyValue(template.Properties, LegacyAIProfileSettingsKey), out MemoryMetadata metadata) ||
            TryDeserialize(GetPropertyValue(template.Properties, LegacyMvcMemorySettingsKey), out metadata))
        {
            return metadata;
        }

        return new MemoryMetadata();
    }

    public static AIProfileTemplate WithMemoryMetadata(this AIProfileTemplate template, MemoryMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(metadata);

        template.Put(metadata);
        template.Properties.Remove(LegacyAIProfileSettingsKey);
        template.Properties.Remove(LegacyMvcMemorySettingsKey);

        return template;
    }

    public static bool TryDeserialize(object value, out MemoryMetadata metadata)
    {
        metadata = Deserialize(value);
        return metadata is not null;
    }

    private static MemoryMetadata Deserialize(object value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is MemoryMetadata metadata)
        {
            return metadata;
        }

        if (value is JsonNode node)
        {
            return node.Deserialize<MemoryMetadata>(_serializerOptions);
        }

        if (value is JsonElement element)
        {
            return element.ValueKind == JsonValueKind.Null
                ? null
                : element.Deserialize<MemoryMetadata>(_serializerOptions);
        }

        var json = JsonSerializer.Serialize(value, _serializerOptions);
        return JsonSerializer.Deserialize<MemoryMetadata>(json, _serializerOptions);
    }

    private static object GetPropertyValue(IDictionary<string, object> properties, string key)
        => properties is not null && properties.TryGetValue(key, out var value)
            ? value
            : null;
}
