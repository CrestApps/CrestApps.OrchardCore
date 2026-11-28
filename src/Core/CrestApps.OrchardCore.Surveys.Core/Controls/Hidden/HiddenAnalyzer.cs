using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Core.Controls.Hidden;

/// <summary>
/// Summarizes a hidden value question.
/// </summary>
public sealed class HiddenAnalyzer : SurveyTextAnalyzerBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HiddenAnalyzer"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public HiddenAnalyzer(IStringLocalizer<HiddenAnalyzer> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string ControlType => SurveyControlNames.Hidden;
}
