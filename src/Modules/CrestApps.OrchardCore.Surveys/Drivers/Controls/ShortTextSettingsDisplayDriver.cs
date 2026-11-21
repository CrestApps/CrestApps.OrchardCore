using CrestApps.OrchardCore.Surveys.Core.Controls.ShortText;
using CrestApps.OrchardCore.Surveys.Core.Drivers;

namespace CrestApps.OrchardCore.Surveys.Drivers.Controls;

/// <summary>
/// Edits the settings of ShortText questions in the designer.
/// </summary>
public sealed class ShortTextSettingsDisplayDriver : SurveyControlSettingsDisplayDriver<ShortTextSettings>
{
    /// <inheritdoc />
    protected override string ControlType => SurveyControlNames.ShortText;
}
