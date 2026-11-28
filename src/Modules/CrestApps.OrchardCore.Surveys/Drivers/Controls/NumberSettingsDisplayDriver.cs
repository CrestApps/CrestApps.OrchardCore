using CrestApps.OrchardCore.Surveys.Core.Controls.Number;
using CrestApps.OrchardCore.Surveys.Core.Drivers;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Drivers.Controls;

/// <summary>
/// Edits the settings of Number questions in the designer.
/// </summary>
public sealed class NumberSettingsDisplayDriver : SurveyControlSettingsDisplayDriver<NumberSettings>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NumberSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public NumberSettingsDisplayDriver(IStringLocalizer<NumberSettingsDisplayDriver> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    protected override string ControlType => SurveyControlNames.Number;

    /// <inheritdoc />
    protected override LocalizedString CardName => S["Number Settings"];
}
