using CrestApps.OrchardCore.Surveys.Core.Controls.Calculated;
using CrestApps.OrchardCore.Surveys.Core.Drivers;

namespace CrestApps.OrchardCore.Surveys.Drivers.Controls;

/// <summary>
/// Edits the settings of Calculated questions in the designer.
/// </summary>
public sealed class CalculatedSettingsDisplayDriver : SurveyControlSettingsDisplayDriver<CalculatedSettings>
{
    /// <inheritdoc />
    protected override string ControlType => SurveyControlNames.Calculated;
}
