using CrestApps.OrchardCore.Surveys.Core.Controls.MultipleChoice;
using CrestApps.OrchardCore.Surveys.Core.Drivers;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Drivers.Controls;

/// <summary>
/// Edits the settings of MultipleChoice questions in the designer.
/// </summary>
public sealed class MultipleChoiceSettingsDisplayDriver : SurveyControlSettingsDisplayDriver<MultipleChoiceSettings>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MultipleChoiceSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public MultipleChoiceSettingsDisplayDriver(IStringLocalizer<MultipleChoiceSettingsDisplayDriver> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    protected override string ControlType => SurveyControlNames.MultipleChoice;

    /// <inheritdoc />
    protected override LocalizedString CardName => S["Multiple Choice Settings"];
}
