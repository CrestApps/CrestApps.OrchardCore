using CrestApps.OrchardCore.Surveys.Controls;

namespace CrestApps.OrchardCore.Surveys.Core.Controls.FillInBlanks;

/// <summary>
/// The settings of a fill in the blanks question.
/// </summary>
public sealed class FillInBlanksSettings
{
    /// <summary>
    /// Gets or sets the text with blanks written as <c>[[answer|other answer]]</c>. It holds the key, so respondents only
    /// see its segments.
    /// </summary>
    [SurveyKey]
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets whether answers compare with case.
    /// </summary>
    public bool CaseSensitive { get; set; }
}
