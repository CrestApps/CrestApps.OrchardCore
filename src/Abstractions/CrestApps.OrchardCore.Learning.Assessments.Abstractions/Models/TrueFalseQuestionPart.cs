using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Assessments.Models;

/// <summary>
/// The answer of a true or false question.
/// </summary>
public sealed class TrueFalseQuestionPart : ContentPart
{
    /// <summary>
    /// Gets or sets whether the statement is true.
    /// </summary>
    public bool Answer { get; set; }
}
