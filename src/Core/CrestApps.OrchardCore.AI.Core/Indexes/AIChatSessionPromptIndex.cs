using CrestApps.OrchardCore.YesSql.Core.Indexes;

namespace CrestApps.OrchardCore.AI.Core.Indexes;

/// <summary>
/// Index for AIChatSessionPrompt documents to enable efficient querying by SessionId.
/// </summary>
public sealed class AIChatSessionPromptIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the SessionId this prompt belongs to.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// Gets or sets the role of the message sender.
    /// </summary>
    public string Role { get; set; }

    /// <summary>
    /// Gets or sets the UTC date and time when the prompt was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}
