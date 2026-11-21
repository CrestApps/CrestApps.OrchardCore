using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Core.Controls.Slider;

/// <summary>
/// Summarizes a slider question.
/// </summary>
public sealed class SliderAnalyzer : SurveyNumberAnalyzerBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SliderAnalyzer"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public SliderAnalyzer(IStringLocalizer<SliderAnalyzer> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string ControlType => SurveyControlNames.Slider;
}
