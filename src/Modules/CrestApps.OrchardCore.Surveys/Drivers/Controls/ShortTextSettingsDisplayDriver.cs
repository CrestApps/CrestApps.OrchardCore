using CrestApps.OrchardCore.Surveys.Core.Controls.ShortText;
using CrestApps.OrchardCore.Surveys.Core.Drivers;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Drivers.Controls;

/// <summary>
/// Edits the settings of ShortText questions in the designer.
/// </summary>
public sealed class ShortTextSettingsDisplayDriver : SurveyControlSettingsDisplayDriver<ShortTextSettings>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ShortTextSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public ShortTextSettingsDisplayDriver(IStringLocalizer<ShortTextSettingsDisplayDriver> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    protected override string ControlType => SurveyControlNames.ShortText;

    /// <inheritdoc />
    protected override LocalizedString CardName => S["Short Text Settings"];
}
