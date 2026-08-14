using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.AI.Core.Models;

/// <summary>
/// Represents the AI profile part.
/// </summary>
public sealed class AIProfilePart : ContentPart
{
    /// <summary>
    /// Gets or sets the profile id.
    /// </summary>
    public string ProfileId { get; set; }

    /// <summary>
    /// Gets or sets the total history.
    /// </summary>
    public int? TotalHistory { get; set; }
}
