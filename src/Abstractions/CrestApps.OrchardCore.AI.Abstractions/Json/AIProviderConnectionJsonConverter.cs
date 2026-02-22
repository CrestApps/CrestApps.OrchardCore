using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI.Json;

public sealed class AIProviderConnectionJsonConverter : JsonConverter<AIProviderConnection>
{
    public override AIProviderConnection Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var node = JsonNode.Parse(ref reader)?.AsObject();

        if (node == null)
        {
            return null;
        }

        var connection = new AIProviderConnection
        {
            ItemId = GetString(node, nameof(AIProviderConnection.ItemId)),
            Source = GetString(node, nameof(AIProviderConnection.Source))
                ?? GetString(node, "ProviderName"),
            Name = GetString(node, nameof(AIProviderConnection.Name)),
            DisplayText = GetString(node, nameof(AIProviderConnection.DisplayText)),
            ChatDeploymentName = GetString(node, nameof(AIProviderConnection.ChatDeploymentName))
                ?? GetString(node, "DefaultDeploymentName"),
            EmbeddingDeploymentName = GetString(node, nameof(AIProviderConnection.EmbeddingDeploymentName))
                ?? GetString(node, "DefaultEmbeddingDeploymentName"),
            ImagesDeploymentName = GetString(node, nameof(AIProviderConnection.ImagesDeploymentName))
                ?? GetString(node, "DefaultImagesDeploymentName"),
            UtilityDeploymentName = GetString(node, nameof(AIProviderConnection.UtilityDeploymentName))
                ?? GetString(node, "DefaultUtilityDeploymentName"),
            IsDefault = GetBool(node, nameof(AIProviderConnection.IsDefault)),
            CreatedUtc = GetDateTime(node, nameof(AIProviderConnection.CreatedUtc)),
            Author = GetString(node, nameof(AIProviderConnection.Author)),
            OwnerId = GetString(node, nameof(AIProviderConnection.OwnerId)),
        };

        if (node.TryGetPropertyValue(nameof(AIProviderConnection.Properties), out var propertiesNode)
            && propertiesNode is JsonObject properties)
        {
            // Detach from parent before assigning.
            node.Remove(nameof(AIProviderConnection.Properties));
            connection.Properties = properties;
        }

        return connection;
    }

    public override void Write(Utf8JsonWriter writer, AIProviderConnection value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        WriteString(writer, nameof(AIProviderConnection.ItemId), value.ItemId);
        WriteString(writer, nameof(AIProviderConnection.Source), value.Source);
        WriteString(writer, nameof(AIProviderConnection.Name), value.Name);
        WriteString(writer, nameof(AIProviderConnection.DisplayText), value.DisplayText);
        WriteString(writer, nameof(AIProviderConnection.ChatDeploymentName), value.ChatDeploymentName);
        WriteString(writer, nameof(AIProviderConnection.EmbeddingDeploymentName), value.EmbeddingDeploymentName);
        WriteString(writer, nameof(AIProviderConnection.ImagesDeploymentName), value.ImagesDeploymentName);
        WriteString(writer, nameof(AIProviderConnection.UtilityDeploymentName), value.UtilityDeploymentName);
        writer.WriteBoolean(nameof(AIProviderConnection.IsDefault), value.IsDefault);
        writer.WriteString(nameof(AIProviderConnection.CreatedUtc), value.CreatedUtc);
        WriteString(writer, nameof(AIProviderConnection.Author), value.Author);
        WriteString(writer, nameof(AIProviderConnection.OwnerId), value.OwnerId);

        writer.WritePropertyName(nameof(AIProviderConnection.Properties));

        if (value.Properties != null)
        {
            value.Properties.WriteTo(writer, options);
        }
        else
        {
            writer.WriteStartObject();
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    private static string GetString(JsonObject node, string name)
    {
        if (node.TryGetPropertyValue(name, out var value) && value != null)
        {
            return value.GetValue<string>();
        }

        return null;
    }

    private static bool GetBool(JsonObject node, string name)
    {
        if (node.TryGetPropertyValue(name, out var value) && value != null)
        {
            return value.GetValue<bool>();
        }

        return false;
    }

    private static DateTime GetDateTime(JsonObject node, string name)
    {
        if (node.TryGetPropertyValue(name, out var value) && value != null)
        {
            return value.GetValue<DateTime>();
        }

        return default;
    }

    private static void WriteString(Utf8JsonWriter writer, string name, string value)
    {
        if (value != null)
        {
            writer.WriteString(name, value);
        }
        else
        {
            writer.WriteNull(name);
        }
    }
}
