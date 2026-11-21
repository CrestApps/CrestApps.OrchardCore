using CrestApps.OrchardCore.Surveys.Core.Controls.Boolean;
using CrestApps.OrchardCore.Surveys.Core.Drivers;

namespace CrestApps.OrchardCore.Surveys.Drivers.Controls;

/// <summary>
/// Edits the settings of Boolean questions in the designer.
/// </summary>
public sealed class BooleanSettingsDisplayDriver : SurveyControlSettingsDisplayDriver<BooleanSettings>
{
    /// <inheritdoc />
    protected override string ControlType => SurveyControlNames.Boolean;
}
