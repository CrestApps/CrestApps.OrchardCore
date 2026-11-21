using CrestApps.OrchardCore.Surveys.Core.Controls.FillInBlanks;
using CrestApps.OrchardCore.Surveys.Core.Drivers;

namespace CrestApps.OrchardCore.Surveys.Drivers.Controls;

/// <summary>
/// Edits the settings of FillInBlanks questions in the designer.
/// </summary>
public sealed class FillInBlanksSettingsDisplayDriver : SurveyControlSettingsDisplayDriver<FillInBlanksSettings>
{
    /// <inheritdoc />
    protected override string ControlType => SurveyControlNames.FillInBlanks;
}
