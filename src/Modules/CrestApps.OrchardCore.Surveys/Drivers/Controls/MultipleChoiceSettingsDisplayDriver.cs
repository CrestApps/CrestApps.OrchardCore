using CrestApps.OrchardCore.Surveys.Core.Controls.MultipleChoice;
using CrestApps.OrchardCore.Surveys.Core.Drivers;

namespace CrestApps.OrchardCore.Surveys.Drivers.Controls;

/// <summary>
/// Edits the settings of MultipleChoice questions in the designer.
/// </summary>
public sealed class MultipleChoiceSettingsDisplayDriver : SurveyControlSettingsDisplayDriver<MultipleChoiceSettings>
{
    /// <inheritdoc />
    protected override string ControlType => SurveyControlNames.MultipleChoice;
}
