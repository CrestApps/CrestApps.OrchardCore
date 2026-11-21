using CrestApps.OrchardCore.Surveys.Core.Controls.SingleChoice;
using CrestApps.OrchardCore.Surveys.Core.Drivers;

namespace CrestApps.OrchardCore.Surveys.Drivers.Controls;

/// <summary>
/// Edits the settings of SingleChoice questions in the designer.
/// </summary>
public sealed class SingleChoiceSettingsDisplayDriver : SurveyControlSettingsDisplayDriver<SingleChoiceSettings>
{
    /// <inheritdoc />
    protected override string ControlType => SurveyControlNames.SingleChoice;
}
