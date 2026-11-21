using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Core.Controls.SingleChoice;

/// <summary>
/// Summarizes a single choice question.
/// </summary>
public sealed class SingleChoiceAnalyzer : SurveyChoiceAnalyzerBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SingleChoiceAnalyzer"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public SingleChoiceAnalyzer(IStringLocalizer<SingleChoiceAnalyzer> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string ControlType => SurveyControlNames.SingleChoice;
}
