namespace CrestApps.AI.Models;

public sealed class InteractionDocumentOptions
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

    public InteractionDocumentOptions Clone()
        => new()
        {
            IndexProfileName = IndexProfileName,
            TopN = TopN,
        };

    public static InteractionDocumentOptions FromSettings(InteractionDocumentSettings settings)
        => settings == null
            ? new InteractionDocumentOptions()
            : new InteractionDocumentOptions
            {
                IndexProfileName = settings.IndexProfileName,
                TopN = settings.TopN,
            };
}
