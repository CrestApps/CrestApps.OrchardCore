using CrestApps.OrchardCore.Surveys.Core.Controls.Calculated;
using CrestApps.OrchardCore.Surveys.Core.Drivers;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Drivers.Controls;

/// <summary>
/// Edits the settings of Calculated questions in the designer.
/// </summary>
public sealed class CalculatedSettingsDisplayDriver : SurveyControlSettingsDisplayDriver<CalculatedSettings>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CalculatedSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public CalculatedSettingsDisplayDriver(IStringLocalizer<CalculatedSettingsDisplayDriver> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    protected override string ControlType => SurveyControlNames.Calculated;

    /// <inheritdoc />
    protected override LocalizedString CardName => S["Calculated Value Settings"];
}
