using CrestApps.OrchardCore.Surveys.Core.Controls.Matching;
using CrestApps.OrchardCore.Surveys.Core.Drivers;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Drivers.Controls;

/// <summary>
/// Edits the settings of Matching questions in the designer.
/// </summary>
public sealed class MatchingSettingsDisplayDriver : SurveyControlSettingsDisplayDriver<MatchingSettings>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MatchingSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public MatchingSettingsDisplayDriver(IStringLocalizer<MatchingSettingsDisplayDriver> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    protected override string ControlType => SurveyControlNames.Matching;

    /// <inheritdoc />
    protected override LocalizedString CardName => S["Matching Settings"];
}
