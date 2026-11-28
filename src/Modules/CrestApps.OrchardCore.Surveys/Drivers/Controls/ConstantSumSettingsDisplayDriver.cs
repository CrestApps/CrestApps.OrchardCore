using CrestApps.OrchardCore.Surveys.Core.Controls.ConstantSum;
using CrestApps.OrchardCore.Surveys.Core.Drivers;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Drivers.Controls;

/// <summary>
/// Edits the settings of ConstantSum questions in the designer.
/// </summary>
public sealed class ConstantSumSettingsDisplayDriver : SurveyControlSettingsDisplayDriver<ConstantSumSettings>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConstantSumSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public ConstantSumSettingsDisplayDriver(IStringLocalizer<ConstantSumSettingsDisplayDriver> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    protected override string ControlType => SurveyControlNames.ConstantSum;

    /// <inheritdoc />
    protected override LocalizedString CardName => S["Constant Sum Settings"];
}
