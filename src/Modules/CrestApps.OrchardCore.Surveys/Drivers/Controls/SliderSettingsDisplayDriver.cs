using CrestApps.OrchardCore.Surveys.Core.Controls.Slider;
using CrestApps.OrchardCore.Surveys.Core.Drivers;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Drivers.Controls;

/// <summary>
/// Edits the settings of Slider questions in the designer.
/// </summary>
public sealed class SliderSettingsDisplayDriver : SurveyControlSettingsDisplayDriver<SliderSettings>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SliderSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public SliderSettingsDisplayDriver(IStringLocalizer<SliderSettingsDisplayDriver> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    protected override string ControlType => SurveyControlNames.Slider;

    /// <inheritdoc />
    protected override LocalizedString CardName => S["Slider Settings"];
}
