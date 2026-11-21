using CrestApps.OrchardCore.Surveys.Core.Controls.Rating;
using CrestApps.OrchardCore.Surveys.Core.Drivers;

namespace CrestApps.OrchardCore.Surveys.Drivers.Controls;

/// <summary>
/// Edits the settings of Rating questions in the designer.
/// </summary>
public sealed class RatingSettingsDisplayDriver : SurveyControlSettingsDisplayDriver<RatingSettings>
{
    /// <inheritdoc />
    protected override string ControlType => SurveyControlNames.Rating;
}
