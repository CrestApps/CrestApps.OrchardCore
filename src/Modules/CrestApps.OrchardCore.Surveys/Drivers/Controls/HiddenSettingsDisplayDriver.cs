using CrestApps.OrchardCore.Surveys.Core.Controls.Hidden;
using CrestApps.OrchardCore.Surveys.Core.Drivers;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Drivers.Controls;

/// <summary>
/// Edits the settings of Hidden questions in the designer.
/// </summary>
public sealed class HiddenSettingsDisplayDriver : SurveyControlSettingsDisplayDriver<HiddenSettings>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HiddenSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public HiddenSettingsDisplayDriver(IStringLocalizer<HiddenSettingsDisplayDriver> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    protected override string ControlType => SurveyControlNames.Hidden;

    /// <inheritdoc />
    protected override LocalizedString CardName => S["Hidden Value Settings"];
}
