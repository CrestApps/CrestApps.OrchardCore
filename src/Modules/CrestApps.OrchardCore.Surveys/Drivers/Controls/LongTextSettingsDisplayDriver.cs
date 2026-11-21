using CrestApps.OrchardCore.Surveys.Core.Controls.LongText;
using CrestApps.OrchardCore.Surveys.Core.Drivers;

namespace CrestApps.OrchardCore.Surveys.Drivers.Controls;

/// <summary>
/// Edits the settings of LongText questions in the designer.
/// </summary>
public sealed class LongTextSettingsDisplayDriver : SurveyControlSettingsDisplayDriver<LongTextSettings>
{
    /// <inheritdoc />
    protected override string ControlType => SurveyControlNames.LongText;
}
