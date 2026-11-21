using CrestApps.OrchardCore.Surveys.Core.Controls.Matching;
using CrestApps.OrchardCore.Surveys.Core.Drivers;

namespace CrestApps.OrchardCore.Surveys.Drivers.Controls;

/// <summary>
/// Edits the settings of Matching questions in the designer.
/// </summary>
public sealed class MatchingSettingsDisplayDriver : SurveyControlSettingsDisplayDriver<MatchingSettings>
{
    /// <inheritdoc />
    protected override string ControlType => SurveyControlNames.Matching;
}
