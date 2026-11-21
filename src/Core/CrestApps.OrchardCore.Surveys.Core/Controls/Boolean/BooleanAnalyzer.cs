using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Core.Controls.Boolean;

/// <summary>
/// Summarizes a yes or no question.
/// </summary>
public sealed class BooleanAnalyzer : SurveyChoiceAnalyzerBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BooleanAnalyzer"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public BooleanAnalyzer(IStringLocalizer<BooleanAnalyzer> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string ControlType => SurveyControlNames.Boolean;

    /// <inheritdoc />
    protected override IEnumerable<(string Id, string Label)> GetOptions(SurveyQuestion question, SurveyAnalysisContext context)
    {
        var settings = question.GetSettings<BooleanSettings>();
        var yes = !string.IsNullOrWhiteSpace(settings.TrueLabel) ? settings.TrueLabel : settings.Display == BooleanDisplay.TrueFalse ? S["True"].Value : S["Yes"].Value;
        var no = !string.IsNullOrWhiteSpace(settings.FalseLabel) ? settings.FalseLabel : settings.Display == BooleanDisplay.TrueFalse ? S["False"].Value : S["No"].Value;

        return [(BooleanValues.True, yes), (BooleanValues.False, no)];
    }
}
