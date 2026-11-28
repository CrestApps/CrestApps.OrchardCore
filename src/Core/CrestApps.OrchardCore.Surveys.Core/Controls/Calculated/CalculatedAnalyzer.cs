using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Core.Controls.Calculated;

/// <summary>
/// Summarizes a calculated value question.
/// </summary>
public sealed class CalculatedAnalyzer : SurveyNumberAnalyzerBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CalculatedAnalyzer"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public CalculatedAnalyzer(IStringLocalizer<CalculatedAnalyzer> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string ControlType => SurveyControlNames.Calculated;
}
