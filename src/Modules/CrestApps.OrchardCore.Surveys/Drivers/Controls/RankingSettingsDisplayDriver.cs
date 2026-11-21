using CrestApps.OrchardCore.Surveys.Core.Controls.Ranking;
using CrestApps.OrchardCore.Surveys.Core.Drivers;

namespace CrestApps.OrchardCore.Surveys.Drivers.Controls;

/// <summary>
/// Edits the settings of Ranking questions in the designer.
/// </summary>
public sealed class RankingSettingsDisplayDriver : SurveyControlSettingsDisplayDriver<RankingSettings>
{
    /// <inheritdoc />
    protected override string ControlType => SurveyControlNames.Ranking;
}
