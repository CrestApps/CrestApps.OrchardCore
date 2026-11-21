using CrestApps.OrchardCore.Surveys.Core.Controls.ConstantSum;
using CrestApps.OrchardCore.Surveys.Core.Drivers;

namespace CrestApps.OrchardCore.Surveys.Drivers.Controls;

/// <summary>
/// Edits the settings of ConstantSum questions in the designer.
/// </summary>
public sealed class ConstantSumSettingsDisplayDriver : SurveyControlSettingsDisplayDriver<ConstantSumSettings>
{
    /// <inheritdoc />
    protected override string ControlType => SurveyControlNames.ConstantSum;
}
