using CrestApps.OrchardCore.Surveys.Core.Controls.SingleChoice;
using CrestApps.OrchardCore.Surveys.Core.Drivers;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Drivers.Controls;

/// <summary>
/// Edits the settings of SingleChoice questions in the designer.
/// </summary>
public sealed class SingleChoiceSettingsDisplayDriver : SurveyControlSettingsDisplayDriver<SingleChoiceSettings>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SingleChoiceSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public SingleChoiceSettingsDisplayDriver(IStringLocalizer<SingleChoiceSettingsDisplayDriver> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    protected override string ControlType => SurveyControlNames.SingleChoice;

    /// <inheritdoc />
    protected override LocalizedString CardName => S["Single Choice Settings"];
}
