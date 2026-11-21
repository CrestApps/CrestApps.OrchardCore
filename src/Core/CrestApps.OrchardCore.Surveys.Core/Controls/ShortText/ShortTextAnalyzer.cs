using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Core.Controls.ShortText;

/// <summary>
/// Summarizes a short text question.
/// </summary>
public sealed class ShortTextAnalyzer : SurveyTextAnalyzerBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ShortTextAnalyzer"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public ShortTextAnalyzer(IStringLocalizer<ShortTextAnalyzer> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string ControlType => SurveyControlNames.ShortText;
}
