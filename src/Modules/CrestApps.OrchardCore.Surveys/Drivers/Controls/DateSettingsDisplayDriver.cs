using CrestApps.OrchardCore.Surveys.Core.Controls.Date;
using CrestApps.OrchardCore.Surveys.Core.Drivers;

namespace CrestApps.OrchardCore.Surveys.Drivers.Controls;

/// <summary>
/// Edits the settings of Date questions in the designer.
/// </summary>
public sealed class DateSettingsDisplayDriver : SurveyControlSettingsDisplayDriver<DateSettings>
{
    /// <inheritdoc />
    protected override string ControlType => SurveyControlNames.Date;
}
