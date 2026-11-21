using CrestApps.OrchardCore.Surveys.Core.Controls.Hidden;
using CrestApps.OrchardCore.Surveys.Core.Drivers;

namespace CrestApps.OrchardCore.Surveys.Drivers.Controls;

/// <summary>
/// Edits the settings of Hidden questions in the designer.
/// </summary>
public sealed class HiddenSettingsDisplayDriver : SurveyControlSettingsDisplayDriver<HiddenSettings>
{
    /// <inheritdoc />
    protected override string ControlType => SurveyControlNames.Hidden;
}
