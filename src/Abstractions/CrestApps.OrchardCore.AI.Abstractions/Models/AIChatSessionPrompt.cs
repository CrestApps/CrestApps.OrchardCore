using CrestApps.OrchardCore.Models;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI.Models;

public sealed class AIChatSessionPrompt : CatalogItem
{
    /// <summary>
    /// Gets or sets the session identifier this prompt belongs to.
    /// </summary>
    public string SessionId { get; set; }

    public ChatRole Role { get; set; }

    public string Content { get; set; }

    public string Title { get; set; }

    public bool IsGeneratedPrompt { get; set; }

    public IEnumerable<string> ContentItemIds { get; set; }

    public bool? UserRating { get; set; }

    public Dictionary<string, AICompletionReference> References { get; set; }

    /// <summary>
    /// Gets or sets the UTC date and time when the prompt was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}
