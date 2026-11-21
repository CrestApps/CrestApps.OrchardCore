namespace CrestApps.OrchardCore.Learning.Assessments.Models;

/// <summary>
/// A prompt and its answer in a matching question.
/// </summary>
public sealed class MatchingPair
{
    /// <summary>
    /// Gets or sets the stable id of the pair.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the prompt.
    /// </summary>
    public string Prompt { get; set; }

    /// <summary>
    /// Gets or sets the answer.
    /// </summary>
    public string Answer { get; set; }
}
