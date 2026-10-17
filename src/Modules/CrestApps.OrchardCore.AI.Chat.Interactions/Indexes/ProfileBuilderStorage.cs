namespace CrestApps.OrchardCore.AI.Chat.Interactions.Indexes;

/// <summary>
/// Storage names used by the AI Profile Builder.
/// </summary>
internal static class ProfileBuilderStorage
{
    /// <summary>
    /// The YesSql collection that holds builder sessions. It is the collection chat interactions are kept in by
    /// default, so a session and its conversation normally live side by side.
    /// </summary>
    public const string CollectionName = "AI";
}
