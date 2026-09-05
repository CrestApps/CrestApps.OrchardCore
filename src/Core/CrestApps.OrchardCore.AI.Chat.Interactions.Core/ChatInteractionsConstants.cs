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
