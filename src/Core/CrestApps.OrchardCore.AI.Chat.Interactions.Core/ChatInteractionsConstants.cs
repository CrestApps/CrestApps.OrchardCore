namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core;

/// <summary>
/// Contains constant values for chat interactions.
/// </summary>
public static class ChatInteractionsConstants
{
    /// <summary>
    /// Represents the feature.
    /// </summary>
    public static class Feature
    {
        public const string ChatInteractions = "CrestApps.OrchardCore.AI.Chat.Interactions";

        public const string ChatDocuments = "CrestApps.OrchardCore.AI.Documents";

        public const string ChatInteractionDocuments = "CrestApps.OrchardCore.AI.Documents.ChatInteractions";
    }

    /// <summary>
    /// Element ids shared between the chat interaction editor shapes and the chat app that drives them.
    /// </summary>
    /// <remarks>
    /// The realtime voice picker is rendered by the settings panel, beside the deployment it applies to,
    /// while the chat app that populates and reads it is configured from the chat shape. A page shows one
    /// interaction, so fixed ids let the two shapes agree without one having to derive the other's prefix.
    /// </remarks>
    public static class ElementIds
    {
        /// <summary>
        /// The container that is revealed once a realtime-capable deployment is selected.
        /// </summary>
        public const string RealtimeVoiceGroup = "realtime-voice-group";

        /// <summary>
        /// The realtime voice picker itself.
        /// </summary>
        public const string RealtimeVoiceSelect = "realtime-voice-select";
    }
}

/// <summary>
/// Namespaced key prefixes for the metadata-driven model parameter inputs collected by the chat interaction
/// SignalR settings hub. Each input is tagged <c>data-setting="&lt;prefix&gt;:&lt;parameterName&gt;"</c>; the hub's
/// <c>ApplyCoreSettingsAsync</c> override reads these and stores them on the interaction metadata.
/// </summary>
public static class ChatInteractionModelParameterSettingKeys
{
    /// <summary>
    /// Prefix for parameters that apply to the chat deployment.
    /// </summary>
    public const string ChatDeployment = "modelParameters";

    /// <summary>
    /// Prefix for parameters that apply to the utility deployment.
    /// </summary>
    public const string UtilityDeployment = "utilityModelParameters";
}
