using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Assessments.Models;

/// <summary>
/// The options of a single or multiple choice question.
/// </summary>
public sealed class ChoiceQuestionPart : ContentPart
{
    /// <summary>
    /// Gets or sets the options.
    /// </summary>
    public IList<ChoiceOption> Options { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the options are shuffled in each attempt.
    /// </summary>
    public bool ShuffleOptions { get; set; } = true;

    /// <summary>
    /// Gets or sets whether a multiple choice answer earns part of the points for each right choice, less each wrong
    /// one, instead of all or nothing.
    /// </summary>
    public bool PartialCredit { get; set; }
}
