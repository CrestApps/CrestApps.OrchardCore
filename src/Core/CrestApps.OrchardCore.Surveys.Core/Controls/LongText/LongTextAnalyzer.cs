using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Core.Controls.LongText;

/// <summary>
/// Summarizes a long text question.
/// </summary>
public sealed class LongTextAnalyzer : SurveyTextAnalyzerBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LongTextAnalyzer"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public LongTextAnalyzer(IStringLocalizer<LongTextAnalyzer> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string ControlType => SurveyControlNames.LongText;
}
