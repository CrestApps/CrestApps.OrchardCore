using CrestApps.OrchardCore.Surveys.Core.Controls.MultipleText;
using CrestApps.OrchardCore.Surveys.Core.Drivers;

namespace CrestApps.OrchardCore.Surveys.Drivers.Controls;

/// <summary>
/// Edits the settings of MultipleText questions in the designer.
/// </summary>
public sealed class MultipleTextSettingsDisplayDriver : SurveyControlSettingsDisplayDriver<MultipleTextSettings>
{
    /// <inheritdoc />
    protected override string ControlType => SurveyControlNames.MultipleText;
}
