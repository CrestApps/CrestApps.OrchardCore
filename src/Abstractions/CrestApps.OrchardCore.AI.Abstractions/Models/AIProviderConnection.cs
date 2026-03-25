using System.Text.Json.Serialization;
using CrestApps.OrchardCore.AI.Json;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Services;

namespace CrestApps.OrchardCore.AI.Models;

[JsonConverter(typeof(AIProviderConnectionJsonConverter))]
public sealed class AIProviderConnection : SourceCatalogEntry, INameAwareModel, IDisplayTextAwareModel, ICloneable<AIProviderConnection>
{
    public string Name { get; set; }

    public string DisplayText { get; set; }

    [Obsolete("Use typed AIDeployment records instead. This property is retained for backward compatibility and migration.")]
    public string ChatDeploymentName { get; set; }

    [Obsolete("Use typed AIDeployment records instead. This property is retained for backward compatibility and migration.")]
    public string EmbeddingDeploymentName { get; set; }

    [Obsolete("Use typed AIDeployment records instead. This property is retained for backward compatibility and migration.")]
    public string ImagesDeploymentName { get; set; }

    [Obsolete("Use typed AIDeployment records instead. This property is retained for backward compatibility and migration.")]
    public string UtilityDeploymentName { get; set; }

    [Obsolete("Use typed AIDeployment records instead. This property is retained for backward compatibility and migration.")]
    public string SpeechToTextDeploymentName { get; set; }

    public bool IsDefault { get; set; }

    /// <summary>
    /// Gets or sets the technical name of the AI client implementation associated with this connection.
    /// This maps to a registered key in <c>AIOptions.Clients</c>.
    /// </summary>
    [JsonIgnore]
    public string ClientName
    {
        get => Source;
        set => Source = value;
    }

    [Obsolete("Use ClientName instead. Retained for backward compatibility.")]
    [JsonIgnore]
    public string ProviderName
    {
        get => Source;
        set => Source = value;
    }

    public DateTime CreatedUtc { get; set; }

    public string Author { get; set; }

    public string OwnerId { get; set; }

    public string GetLegacyChatDeploymentName()
    {
#pragma warning disable CS0618 // Type or member is obsolete
        return ChatDeploymentName;
#pragma warning restore CS0618 // Type or member is obsolete
    }

    public string GetLegacyEmbeddingDeploymentName()
    {
#pragma warning disable CS0618 // Type or member is obsolete
        return EmbeddingDeploymentName;
#pragma warning restore CS0618 // Type or member is obsolete
    }

    public string GetLegacyImageDeploymentName()
    {
#pragma warning disable CS0618 // Type or member is obsolete
        return ImagesDeploymentName;
#pragma warning restore CS0618 // Type or member is obsolete
    }

    public string GetLegacyUtilityDeploymentName()
    {
#pragma warning disable CS0618 // Type or member is obsolete
        return UtilityDeploymentName;
#pragma warning restore CS0618 // Type or member is obsolete
    }

    public AIProviderConnection Clone()
    {
        return new AIProviderConnection
        {
            ItemId = ItemId,
            Source = Source,
            Name = Name,
            DisplayText = DisplayText,
            IsDefault = IsDefault,
#pragma warning disable CS0618 // Type or member is obsolete
            ChatDeploymentName = ChatDeploymentName,
            EmbeddingDeploymentName = EmbeddingDeploymentName,
            ImagesDeploymentName = ImagesDeploymentName,
            UtilityDeploymentName = UtilityDeploymentName,
            SpeechToTextDeploymentName = SpeechToTextDeploymentName,
#pragma warning restore CS0618
            CreatedUtc = CreatedUtc,
            Author = Author,
            OwnerId = OwnerId,
            Properties = Properties.DeepClone().AsObject(),
        };
    }
}
