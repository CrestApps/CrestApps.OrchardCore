using CrestApps.OrchardCore.Surveys.Core.Controls.Boolean;
using CrestApps.OrchardCore.Surveys.Core.Drivers;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Drivers.Controls;

/// <summary>
/// Edits the settings of Boolean questions in the designer.
/// </summary>
public sealed class BooleanSettingsDisplayDriver : SurveyControlSettingsDisplayDriver<BooleanSettings>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BooleanSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public BooleanSettingsDisplayDriver(IStringLocalizer<BooleanSettingsDisplayDriver> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    protected override string ControlType => SurveyControlNames.Boolean;

    /// <inheritdoc />
    protected override LocalizedString CardName => S["Yes or No Settings"];
}
