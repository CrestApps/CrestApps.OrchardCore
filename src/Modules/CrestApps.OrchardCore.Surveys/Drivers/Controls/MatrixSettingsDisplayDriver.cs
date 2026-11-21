using CrestApps.OrchardCore.Surveys.Core.Controls.Matrix;
using CrestApps.OrchardCore.Surveys.Core.Drivers;

namespace CrestApps.OrchardCore.Surveys.Drivers.Controls;

/// <summary>
/// Edits the settings of Matrix questions in the designer.
/// </summary>
public sealed class MatrixSettingsDisplayDriver : SurveyControlSettingsDisplayDriver<MatrixSettings>
{
    /// <inheritdoc />
    protected override string ControlType => SurveyControlNames.Matrix;
}
