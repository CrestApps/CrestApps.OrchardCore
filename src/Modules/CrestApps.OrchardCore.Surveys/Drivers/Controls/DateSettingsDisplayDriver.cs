using CrestApps.OrchardCore.Surveys.Core.Controls.Date;
using CrestApps.OrchardCore.Surveys.Core.Drivers;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Drivers.Controls;

/// <summary>
/// Edits the settings of Date questions in the designer.
/// </summary>
public sealed class DateSettingsDisplayDriver : SurveyControlSettingsDisplayDriver<DateSettings>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DateSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public DateSettingsDisplayDriver(IStringLocalizer<DateSettingsDisplayDriver> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    protected override string ControlType => SurveyControlNames.Date;

    /// <inheritdoc />
    protected override LocalizedString CardName => S["Date and Time Settings"];
}
