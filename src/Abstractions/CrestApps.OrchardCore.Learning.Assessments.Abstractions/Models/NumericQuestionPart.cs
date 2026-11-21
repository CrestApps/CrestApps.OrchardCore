using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Assessments.Models;

/// <summary>
/// The answer of a numeric question.
/// </summary>
public sealed class NumericQuestionPart : ContentPart
{
    /// <summary>
    /// Gets or sets the answer.
    /// </summary>
    public decimal Answer { get; set; }

    /// <summary>
    /// Gets or sets how far from the answer a response may be and still count.
    /// </summary>
    public decimal Tolerance { get; set; }
}
