using CrestApps.OrchardCore.Surveys.Core.Controls.LongText;
using CrestApps.OrchardCore.Surveys.Core.Drivers;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Drivers.Controls;

/// <summary>
/// Edits the settings of LongText questions in the designer.
/// </summary>
public sealed class LongTextSettingsDisplayDriver : SurveyControlSettingsDisplayDriver<LongTextSettings>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LongTextSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public LongTextSettingsDisplayDriver(IStringLocalizer<LongTextSettingsDisplayDriver> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    protected override string ControlType => SurveyControlNames.LongText;

    /// <inheritdoc />
    protected override LocalizedString CardName => S["Long Text Settings"];
}
