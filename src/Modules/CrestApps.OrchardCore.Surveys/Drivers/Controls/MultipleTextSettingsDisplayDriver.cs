using CrestApps.OrchardCore.Surveys.Core.Controls.MultipleText;
using CrestApps.OrchardCore.Surveys.Core.Drivers;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Drivers.Controls;

/// <summary>
/// Edits the settings of MultipleText questions in the designer.
/// </summary>
public sealed class MultipleTextSettingsDisplayDriver : SurveyControlSettingsDisplayDriver<MultipleTextSettings>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MultipleTextSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public MultipleTextSettingsDisplayDriver(IStringLocalizer<MultipleTextSettingsDisplayDriver> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    protected override string ControlType => SurveyControlNames.MultipleText;

    /// <inheritdoc />
    protected override LocalizedString CardName => S["Multiple Text Settings"];
}
