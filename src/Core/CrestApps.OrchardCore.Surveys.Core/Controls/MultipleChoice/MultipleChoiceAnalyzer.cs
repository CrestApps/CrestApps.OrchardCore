using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Core.Controls.MultipleChoice;

/// <summary>
/// Summarizes a multiple choice question.
/// </summary>
public sealed class MultipleChoiceAnalyzer : SurveyChoiceAnalyzerBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MultipleChoiceAnalyzer"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public MultipleChoiceAnalyzer(IStringLocalizer<MultipleChoiceAnalyzer> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string ControlType => SurveyControlNames.MultipleChoice;
}
