using CrestApps.OrchardCore.Surveys.Core.Controls.Slider;
using CrestApps.OrchardCore.Surveys.Core.Drivers;

namespace CrestApps.OrchardCore.Surveys.Drivers.Controls;

/// <summary>
/// Edits the settings of Slider questions in the designer.
/// </summary>
public sealed class SliderSettingsDisplayDriver : SurveyControlSettingsDisplayDriver<SliderSettings>
{
    /// <inheritdoc />
    protected override string ControlType => SurveyControlNames.Slider;
}
