using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Assessments.Models;

/// <summary>
/// The accepted answers of a short text question.
/// </summary>
public sealed class ShortTextQuestionPart : ContentPart
{
    /// <summary>
    /// Gets or sets the accepted answers.
    /// </summary>
    public IList<string> AcceptedAnswers { get; set; } = [];

    /// <summary>
    /// Gets or sets whether letter case matters.
    /// </summary>
    public bool CaseSensitive { get; set; }
}
