using CrestApps.OrchardCore.Surveys.Core.Controls.Ranking;
using CrestApps.OrchardCore.Surveys.Core.Drivers;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Drivers.Controls;

/// <summary>
/// Edits the settings of Ranking questions in the designer.
/// </summary>
public sealed class RankingSettingsDisplayDriver : SurveyControlSettingsDisplayDriver<RankingSettings>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RankingSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public RankingSettingsDisplayDriver(IStringLocalizer<RankingSettingsDisplayDriver> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    protected override string ControlType => SurveyControlNames.Ranking;

    /// <inheritdoc />
    protected override LocalizedString CardName => S["Ranking Settings"];
}
