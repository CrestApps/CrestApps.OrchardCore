namespace CrestApps.OrchardCore.AI.Chat.Interactions.Drivers;

public sealed class InteractionDocumentSettings
{
    /// <summary>
    /// Gets or sets the index profile name to use for document embedding and search.
    /// </summary>
    public string IndexProfileName { get; set; }

    /// <summary>
    /// Gets or sets the number of top matching document chunks to include in AI context.
    /// Default is 3.
    /// </summary>
    public int TopN { get; set; } = 3;
}
