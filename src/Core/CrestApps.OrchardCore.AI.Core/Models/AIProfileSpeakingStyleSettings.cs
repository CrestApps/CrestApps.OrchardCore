namespace CrestApps.OrchardCore.AI.Core.Models;

/// <summary>
/// The speaking styles an AI profile follows, each a tested prompt section added to its system message.
/// </summary>
public sealed class AIProfileSpeakingStyleSettings
{
    /// <summary>
    /// Gets or sets the identifiers of the chosen styles, from <see cref="AISpeakingStyles"/>.
    /// </summary>
    public string[] Styles { get; set; } = [];
}
