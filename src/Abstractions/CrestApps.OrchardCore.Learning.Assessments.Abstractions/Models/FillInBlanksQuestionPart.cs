using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Assessments.Models;

/// <summary>
/// A text with blanks. Each blank is written in the text as <c>[[answer]]</c>, or <c>[[answer|other answer]]</c> for
/// several accepted answers.
/// </summary>
public sealed class FillInBlanksQuestionPart : ContentPart
{
    /// <summary>
    /// Gets or sets the text with its blanks.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets whether letter case matters.
    /// </summary>
    public bool CaseSensitive { get; set; }
}
