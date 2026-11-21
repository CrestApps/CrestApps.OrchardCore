using CrestApps.OrchardCore.Surveys.Core.Controls.Number;
using CrestApps.OrchardCore.Surveys.Core.Drivers;

namespace CrestApps.OrchardCore.Surveys.Drivers.Controls;

/// <summary>
/// Edits the settings of Number questions in the designer.
/// </summary>
public sealed class NumberSettingsDisplayDriver : SurveyControlSettingsDisplayDriver<NumberSettings>
{
    /// <inheritdoc />
    protected override string ControlType => SurveyControlNames.Number;
}
