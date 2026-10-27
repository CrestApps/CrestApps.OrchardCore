namespace CrestApps.OrchardCore.AI.Core.Models;

/// <summary>
/// The speaking styles an AI profile follows, each a tested prompt section added to its system message.
/// </summary>
public sealed class AIProfileSpeakingStyleSettings
{
    /// <summary>
    /// Gets or sets the prompt templates of the chosen styles, in the <see cref="AISpeakingStyles.Category"/>
    /// category, in the order their sections are added.
    /// </summary>
    public string[] Styles { get; set; } = [];
}
