using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Core.Controls.Number;

/// <summary>
/// Summarizes a number question.
/// </summary>
public sealed class NumberAnalyzer : SurveyNumberAnalyzerBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NumberAnalyzer"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public NumberAnalyzer(IStringLocalizer<NumberAnalyzer> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string ControlType => SurveyControlNames.Number;
}
