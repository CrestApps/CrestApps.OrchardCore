namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models;

/// <summary>
/// Options for configuring document processing intents and their descriptions.
/// Use this to register custom intents that will be recognized by the AI intent detector.
/// </summary>
public sealed class PromptProcessingOptions
{
    /// <summary>
    /// Gets the dictionary of registered intents and their descriptions.
    /// The key is the intent name (e.g., "DocumentQnA") and the value is the description
    /// used by the AI intent detector to classify user prompts.
    /// </summary>
    /// <remarks>
    /// Only intents registered here will be recognized by the AI intent detector and
    /// processed by their corresponding strategies. To add a custom intent:
    /// <code>
    /// services.Configure&lt;DocumentProcessingOptions&gt;(o =&gt;
    /// {
    ///     o.Intents.TryAdd("MyCustomIntent", "Description of when this intent should be detected.");
    /// });
    /// </code>
    /// </remarks>
    internal Dictionary<string, string> InternalIntents { get; } = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, string> Intents => InternalIntents;
}
