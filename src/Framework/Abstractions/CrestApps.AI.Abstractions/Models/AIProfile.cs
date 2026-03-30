using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.Models;
using CrestApps.Services;

namespace CrestApps.AI.Models;

public sealed class AIProfile : SourceCatalogEntry, INameAwareModel, IDisplayTextAwareModel, ICloneable<AIProfile>
{
    /// <summary>
    /// Gets or sets the technical name of the profile.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the display text of the profile.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets the type of AI chat profile.
    /// </summary>
    public AIProfileType Type { get; set; }

    /// <summary>
    /// Gets or sets a description of the profile's capabilities.
    /// Required for <see cref="AIProfileType.Agent"/> profiles, where it describes
    /// what the agent can do so the orchestrator can decide when to invoke it.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the legacy connection name for the profile.
    /// Retained for backward compatibility with older stored profiles.
    /// </summary>
    [Obsolete("Use ChatDeploymentId and UtilityDeploymentId. The selected deployment determines the connection.")]
    public string ConnectionName { get; set; }

    /// <summary>
    /// Gets or sets the chat deployment identifier for this profile.
    /// </summary>
    public string ChatDeploymentId { get; set; }

    /// <summary>
    /// Gets or sets the denormalized chat deployment name for this profile.
    /// Populated automatically from the deployment when the profile is saved.
    /// </summary>
    public string ChatDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the utility deployment identifier for this profile.
    /// When not set, falls back to the global default utility deployment.
    /// </summary>
    public string UtilityDeploymentId { get; set; }

    [Obsolete("Use ChatDeploymentId instead. Retained for backward compatibility.")]
    [JsonIgnore]
    public string DeploymentId
    {
        get => ChatDeploymentId;
        set => ChatDeploymentId = value;
    }

    [JsonInclude]
    [JsonPropertyName("DeploymentId")]
    private string _deploymentIdBackingField
    {
        set => ChatDeploymentId = value;
    }

    /// <summary>
    /// Gets or sets the type of title used in the session.
    /// </summary>
    public AISessionTitleType? TitleType { get; set; }

    /// <summary>
    /// Gets or sets the welcome message shown to users.
    /// </summary>
    public string WelcomeMessage { get; set; }

    /// <summary>
    /// Gets or sets the subject of the prompt.
    /// </summary>
    public string PromptSubject { get; set; }

    /// <summary>
    /// Gets or sets the template for the prompt.
    /// </summary>
    public string PromptTemplate { get; set; }

    /// <summary>
    /// Gets or sets the name of the orchestrator to use for this profile.
    /// When <see langword="null"/> or empty, the system default orchestrator is used.
    /// </summary>
    public string OrchestratorName { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the profile was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the owner of this profile.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the author of this profile.
    /// </summary>
    public string Author { get; set; }

    /// <summary>
    /// Gets or sets the JSON-based settings for the profile.
    /// </summary>
    public JsonObject Settings { get; init; } = [];

    public string GetLegacyConnectionName()
    {
#pragma warning disable CS0618 // Type or member is obsolete
        return ConnectionName;
#pragma warning restore CS0618 // Type or member is obsolete
    }

    /// <summary>
    /// Creates a deep copy of the current profile.
    /// </summary>
    /// <returns>A cloned instance of <see cref="AIProfile"/>.</returns>
    public AIProfile Clone()
    {
        return new AIProfile()
        {
            ItemId = ItemId,
            Name = Name,
            Source = Source,
            DisplayText = DisplayText,
            Type = Type,
            Description = Description,
            OrchestratorName = OrchestratorName,
#pragma warning disable CS0618 // Type or member is obsolete
            ConnectionName = ConnectionName,
#pragma warning restore CS0618 // Type or member is obsolete
            ChatDeploymentId = ChatDeploymentId,
            ChatDeploymentName = ChatDeploymentName,
            UtilityDeploymentId = UtilityDeploymentId,
            TitleType = TitleType,
            WelcomeMessage = WelcomeMessage,
            PromptSubject = PromptSubject,
            PromptTemplate = PromptTemplate,
            CreatedUtc = CreatedUtc,
            OwnerId = OwnerId,
            Author = Author,
            Properties = Properties.Clone(),
            Settings = Settings.Clone(),
        };
    }

    public override string ToString()
    {
        if (string.IsNullOrEmpty(DisplayText))
        {
            return Name;
        }

        return DisplayText;
    }
}
