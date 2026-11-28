using CrestApps.OrchardCore.Surveys.Core.Controls.Rating;
using CrestApps.OrchardCore.Surveys.Core.Drivers;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Drivers.Controls;

/// <summary>
/// Edits the settings of Rating questions in the designer.
/// </summary>
public sealed class RatingSettingsDisplayDriver : SurveyControlSettingsDisplayDriver<RatingSettings>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RatingSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public RatingSettingsDisplayDriver(IStringLocalizer<RatingSettingsDisplayDriver> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    protected override string ControlType => SurveyControlNames.Rating;

    /// <inheritdoc />
    protected override LocalizedString CardName => S["Rating Settings"];
}
