using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Assessments.Models;

/// <summary>
/// The pairs of a matching question.
/// </summary>
public sealed class MatchingQuestionPart : ContentPart
{
    /// <summary>
    /// Gets or sets the pairs.
    /// </summary>
    public IList<MatchingPair> Pairs { get; set; } = [];

    /// <summary>
    /// Gets or sets whether each right match earns its share of the points.
    /// </summary>
    public bool PartialCredit { get; set; } = true;
}
