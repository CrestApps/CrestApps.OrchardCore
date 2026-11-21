namespace CrestApps.OrchardCore.Surveys.Core.Controls.Matching;

/// <summary>
/// The settings of a matching question. Its prompts are the question's rows and its answers the question's choices.
/// </summary>
public sealed class MatchingSettings
{
    /// <summary>
    /// Gets or sets whether the answers show in a random order.
    /// </summary>
    public bool ShuffleAnswers { get; set; } = true;
}
